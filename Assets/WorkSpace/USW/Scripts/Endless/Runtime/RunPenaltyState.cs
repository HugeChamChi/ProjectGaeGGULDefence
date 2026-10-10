using System;
using System.Collections.Generic;

/// <summary>런의 후보 추첨·선택·다음 라운드 적용을 분리한다.</summary>
internal sealed class RunPenaltyState
{
    private sealed class Candidate { internal RunPenaltyData Data; internal double Weight; internal int Count; }
    private readonly IEndlessRandom _random;
    private readonly List<Candidate> _candidates = new List<Candidate>();
    private readonly List<RunPenaltyData> _offered = new List<RunPenaltyData>();
    private Candidate _pending;
    internal double Attack { get; private set; } = 1d;
    internal double Frequency { get; private set; } = 1d;
    internal double Experience { get; private set; } = 1d;
    internal int ChoiceReduction { get; private set; }
    internal bool PermanentSeal { get; private set; }
    internal IReadOnlyList<RunPenaltyData> Offered => _offered.AsReadOnly();
    internal bool AwaitingChoice => _offered.Count > 0 && _pending == null;
    internal RunPenaltyState(IEndlessRandom random) => _random = random;
    internal void Reset(RunPenaltyPoolData pool)
    {
        DiscardPending(); _candidates.Clear(); Attack = Frequency = Experience = 1d;
        ChoiceReduction = 0; PermanentSeal = false;
        if (pool == null) return;
        foreach (var entry in pool.Entries)
            if (entry.Enabled) _candidates.Add(new Candidate { Data = entry.Penalty, Weight = entry.Weight });
    }
    internal int GetStackCount(string key)
    { foreach (var c in _candidates) if(c.Data.Key == key) return c.Count; return 0; }
    internal bool TryOffer(out string error)
    {
        error = "Invalid penalty candidate draw.";
        if (_pending != null || _offered.Count != 0) return false;
        var eligible = _candidates.FindAll(c => c.Count < int.MaxValue && (!c.Data.OncePerRun || c.Count == 0));
        if (eligible.Count < 2) return false;
        for (int i=0;i<2;i++)
        {
            double total=0; foreach(var c in eligible) total+=c.Weight;
            double sample=_random.NextUnit();
            if(sample<0 || sample>=1 || double.IsNaN(sample)) { _offered.Clear(); return false; }
            double point=sample*total; var selected=eligible[eligible.Count-1];
            foreach(var c in eligible) { point-=c.Weight; if(point<0) { selected=c; break; } }
            _offered.Add(selected.Data); eligible.Remove(selected);
        }
        error=""; return true;
    }
    internal bool TryChoose(long runId, int nextRound, string key, out RunPenaltyResult result)
    {
        result=default;
        if(!AwaitingChoice || !_offered.Exists(d=>d.Key==key)) return false;
        _pending=_candidates.Find(c=>c.Data.Key==key);
        result=new RunPenaltyResult(runId,key,_pending.Count+1,nextRound); return true;
    }
    internal bool TryActivate(out bool changed,out string error)
    {
        changed=false;error="";
        if(AwaitingChoice) {error="Penalty choice is still pending.";return false;}
        if(_pending==null)return true;
        double attack=1,frequency=1,experience=1;int reduction=0;bool seal=false;
        foreach(var c in _candidates)
        {
            int count=c.Count+(c==_pending?1:0);
            double factor=Math.Pow(1+c.Data.Delta/100d,count);
            switch(c.Data.Target)
            {
                case RunPenaltyTarget.UnitAttack:attack*=factor;break;
                case RunPenaltyTarget.UnitAttackFrequency:frequency*=factor;break;
                case RunPenaltyTarget.ExperienceGain:experience*=factor;break;
                case RunPenaltyTarget.ChoiceReduction:reduction+=(int)(-c.Data.Delta)*count;break;
                case RunPenaltyTarget.PermanentCellSeal:seal|=count>0;break;
            }
        }
        if(!(attack>0)||!(frequency>0)||!(experience>0)||double.IsInfinity(attack)||double.IsInfinity(frequency))
        {error="Run multiplier overflow/underflow.";return false;}
        _pending.Count++;Attack=attack;Frequency=frequency;Experience=experience;ChoiceReduction=reduction;PermanentSeal=seal;
        DiscardPending();changed=true;return true;
    }
    internal void DiscardPending() { _pending=null;_offered.Clear(); }
}

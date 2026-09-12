using System;
using System.Collections.Generic;
using System.Linq;

internal static class Program
{
    private static int _passed;
    private static void Check(string name, Action action)
    { action(); _passed++; Console.WriteLine("PASS " + name); }
    private static void Equal<T>(T expected, T actual)
    { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
    private static void Throws(Action action)
    { try { action(); } catch (ArgumentException) { return; } catch (FormatException) { return; } throw new Exception("Expected rejection"); }
    private static DebuffDefinition Burn(double duration = 5, double interval = 1) =>
        new DebuffDefinition(1001, "DBF_BURN_001", DebuffKind.Burn, "BURN", duration, 1, interval, .01m, 1);
    private static DebuffDefinition Armor() => new DebuffDefinition(1002, "DBF_ARMOR_BREAK_001", DebuffKind.ArmorBreak, "ARMOR_BREAK", 0, 50, armorStrength: .3);
    private static DebuffDefinition Taken() => new DebuffDefinition(1003, "DBF_DAMAGE_TAKEN_UP_001", DebuffKind.DamageTakenIncrease, "DAMAGE_TAKEN_UP", 5, 1, damageMultiplier: 1.2m);
    private static (CombatHealth hp, DebuffController controller, List<long> ticks) Target(decimal initial)
    {
        var hp = new CombatHealth(); hp.Reset(initial);
        var ticks = new List<long>();
        var controller = new DebuffController(units => { ticks.Add(hp.ApplyDamage(units)); }, () => !hp.IsDead);
        return (hp, controller, ticks);
    }
    private static void Apply(DebuffController c, DebuffDefinition d, long snapshot, int source = 1, int stacks = 1)
        => c.Apply(d, new DebuffApplyContext(source, stacks, snapshot));
    private static void Main()
    {
        Check("4 digit midpoint away from zero", () => Equal(12346L, CombatHealth.ToUnits(1.23455m)));
        Check("maximum supported HP preserves 0.0001", () => {
            var h = new CombatHealth(); h.Reset(CombatHealth.MaximumHp); h.ApplyDamage(1);
            Equal(long.MaxValue - 1, h.CurrentUnits); Equal(CombatHealth.MaximumHp - .0001m, h.Current);
        });
        Check("range overflow and negative HP rejected", () => { Throws(() => CombatHealth.ToUnits(CombatHealth.MaximumHp + .0001m)); Throws(() => CombatHealth.ToUnits(-1)); });
        Check("positive last unit alive, exact zero dead", () => { var h = new CombatHealth(); h.Reset(.0001m); Equal(false, h.IsDead); Equal(1L, h.ApplyDamage(long.MaxValue)); Equal(true, h.IsDead); });
        Check("ordinary damage zero defense and multiplier once", () => Equal(1200000L, DamageCalculator.Calculate(100, 0, 500, 1, 1.2m, long.MaxValue)));
        Check("defense D=K matches independent numeric reference", () => Equal(590616L, DamageCalculator.Calculate(100, 500, 500, 1, 1, long.MaxValue)));
        Check("high defense improves with capped armor", () => { long a = DamageCalculator.Calculate(100, 50000, 500, 1, 1, long.MaxValue); long b = DamageCalculator.Calculate(100, 50000, 500, 1.3, 1, long.MaxValue); if (b <= a || (decimal)b / a >= 1.3m) throw new Exception("Armor curve"); });
        Check("overkill capped before fixed conversion overflow", () => Equal(123L, DamageCalculator.Calculate(decimal.MaxValue, 0, 500, 1, 1.2m, 123)));
        Check("tiny ordinary damage rounds to zero, no invented minimum", () => Equal(0L, DamageCalculator.Calculate(.00001m, 0, 500, 1, 1, 10000)));
        Check("invalid defense rejected", () => { Throws(() => DamageCalculator.Calculate(1, double.NaN, 500, 1, 1, 10)); Throws(() => DamageCalculator.Calculate(1, 0, 0, 1, 1, 10)); });
        Check("burn first tick delayed, five fractional ticks preserved", () => { var t = Target(1250); Apply(t.controller, Burn(), t.hp.CurrentUnits); t.controller.Advance(.999); Equal(0, t.ticks.Count); t.controller.Advance(5); Equal(5, t.ticks.Count); Equal(true, t.ticks.All(x => x == 25000)); Equal(1237.5m, t.hp.Current); Equal(0, t.controller.Active.Count); });
        Check("burn min tick one", () => { var t = Target(100); Apply(t.controller, Burn(), t.hp.CurrentUnits); t.controller.Advance(5); Equal(95m, t.hp.Current); });
        Check("three ticks preserve total with remainder on last", () => { var t = Target(10000); Apply(t.controller, Burn(3), t.hp.CurrentUnits); t.controller.Advance(3); Equal(1000000L, t.ticks.Sum()); Equal(9900m, t.hp.Current); });
        Check("six tick rounding uses nearest and last correction", () => { var t=Target(10000); Apply(t.controller,Burn(6),t.hp.CurrentUnits); t.controller.Advance(6); Equal(166667L,t.ticks[0]); Equal(166665L,t.ticks[5]); Equal(1000000L,t.ticks.Sum()); });
        Check("rounding correction cannot violate minimum tick", () => { var t=Target(600.04m); Apply(t.controller,Burn(6),t.hp.CurrentUnits); t.controller.Advance(6); Equal(60004L,t.ticks.Sum()); Equal(true,t.ticks.All(x=>x>=10000)); });
        Check("zero damage stays zero with extreme multiplier", () => Equal(0L,DamageCalculator.Calculate(0,0,500,1,decimal.MaxValue,1)));
        Check("burn independent of armor and vulnerability", () => { var t = Target(1250); Apply(t.controller, Armor(), t.hp.CurrentUnits, stacks:50); Apply(t.controller, Taken(), t.hp.CurrentUnits); Apply(t.controller, Burn(), t.hp.CurrentUnits); t.controller.Advance(1); Equal(25000L, t.ticks.Single()); });
        Check("refresh replaces snapshot but retains next tick", () => { var t = Target(10000); Apply(t.controller, Burn(), t.hp.CurrentUnits); t.controller.Advance(.5); Apply(t.controller, Burn(), CombatHealth.ToUnits(1250), 2); t.controller.Advance(1); Equal(25000L, t.ticks.Single()); Equal(2, t.controller.Active.Single().SourceId); });
        Check("frequent refresh cannot starve ticks", () => { var t = Target(10000); Apply(t.controller, Burn(), t.hp.CurrentUnits); for (int i=1;i<=50;i++) { t.controller.Advance(i/10.0); Apply(t.controller, Burn(), t.hp.CurrentUnits, i); } Equal(5, t.ticks.Count); Equal(1, t.controller.Active.Count); });
        Check("expiry tick before same-time next application", () => { var t = Target(10000); Apply(t.controller, Burn(), t.hp.CurrentUnits); t.controller.Advance(5); Equal(5,t.ticks.Count); long afterOld = t.hp.CurrentUnits; Apply(t.controller,Burn(),afterOld); t.controller.Advance(6); Equal(198000L,t.ticks.Last()); });
        Check("vulnerability refresh stays 1.2 not multiplicative", () => { var t=Target(100); Apply(t.controller,Taken(),t.hp.CurrentUnits); t.controller.Advance(4); Apply(t.controller,Taken(),t.hp.CurrentUnits,2); t.controller.Advance(5); Equal(1.2m,t.controller.DamageTakenMultiplier); t.controller.Advance(9); Equal(1m,t.controller.DamageTakenMultiplier); });
        Check("armor shared across sources and saturates without overflow", () => { var t=Target(100); for(int i=0;i<51;i++) Apply(t.controller,Armor(),t.hp.CurrentUnits,i); Apply(t.controller,Armor(),t.hp.CurrentUnits,stacks:int.MaxValue); Equal(50,t.controller.Active.Single().Stacks); Equal(1.3,t.controller.ArmorFactor); t.controller.Advance(1000); Equal(50,t.controller.Active.Single().Stacks); });
        Check("separate boss has no debuffs", () => { var a=Target(100); var b=Target(100); Apply(a.controller,Taken(),a.hp.CurrentUnits); Equal(1m,b.controller.DamageTakenMultiplier); a.controller.Clear(); Equal(1m,a.controller.DamageTakenMultiplier); });
        Check("death during catch-up stops ticks", () => { var t=Target(2); Apply(t.controller,Burn(),t.hp.CurrentUnits); t.controller.Advance(5); Equal(2,t.ticks.Count); Equal(0,t.controller.Active.Count); Equal(false,t.controller.Apply(Taken(),new DebuffApplyContext(1,1,0))); });
        Check("pause with unchanged clock yields no tick", () => { var t=Target(100); Apply(t.controller,Burn(),t.hp.CurrentUnits); for(int i=0;i<100;i++)t.controller.Advance(0); Equal(0,t.ticks.Count); });
        Check("nonintegral duration and invalid burn rejected", () => { Throws(()=>Burn(2.5)); Throws(()=>Burn(5,0)); });
        Check("fractional tick interval does not drop expiry tick", () => { var t=Target(10000); Apply(t.controller,Burn(.3,.1),t.hp.CurrentUnits); t.controller.Advance(.3); Equal(3,t.ticks.Count); Equal(1000000L,t.ticks.Sum()); Equal(0,t.controller.Active.Count); });
        Check("binding is a value copy and tier lookup independent", () => { var tiers=new PerTierDebuffBinding(); tiers.Set(Tier.Normal,new DebuffBinding(1003,DebuffTrigger.SkillActivated,1)); var b=tiers.Get(Tier.Normal); b=default; Equal(1003,tiers.Get(Tier.Normal).DebuffId); Equal(false,tiers.Get(Tier.Legend).IsConfigured); });
        Check("catalog duplicate ID/key/group/kind rejected", () => Throws(()=>DebuffCatalog.Validate(new[]{Taken(),Taken()})));
        string[] headers="debuff_id,debuff_key,kind,stack_group,lifetime,duration_sec,max_stacks,reapply_policy,tick_interval_sec,snapshot_hp_percent,min_tick_damage,armor_strength,damage_taken_multiplier".Split(',');
        string[] row="1001,DBF_BURN_001,Burn,BURN,Timed,5,1,Refresh,1,1,1,,".Split(',');
        Check("sheet percent conversion and reordered headers", () => { var d=DebuffSheetParser.Parse(new[]{headers.Reverse().ToArray(),row.Reverse().ToArray()}).Single(); Equal(.01m,d.SnapshotRatio); Equal(5,d.TickCount); });
        Check("sheet unknown kind and missing required header rejected", () => { var bad=(string[])row.Clone(); bad[2]="Typo"; Throws(()=>DebuffSheetParser.Parse(new[]{headers,bad})); Throws(()=>DebuffSheetParser.Parse(new[]{headers.Skip(1).ToArray(),row.Skip(1).ToArray()})); });
        Check("legacy absent FK and explicit empty FK differ", () => { Equal(false,DebuffSheetParser.ParseBinding(new[]{"name"},new[]{"a"},true,"unit").HasValue); var b=DebuffSheetParser.ParseBinding(new[]{"debuff_id","debuff_trigger","debuff_stacks_per_apply"},new[]{"","",""},true,"unit"); Equal(true,b.HasValue); Equal(false,b.Value.IsConfigured); });
        Check("binding validates source trigger and shared one stack", () => { string[] h={"debuff_id","debuff_trigger","debuff_stacks_per_apply"}; Throws(()=>DebuffSheetParser.ParseBinding(h,new[]{"1002","AffectedUnitBasicAttackAttempt","2"},false,"totem")); Throws(()=>DebuffSheetParser.ParseBinding(h,new[]{"1003","ProjectileHit","1"},true,"unit")); });
        Check("deadline burn kill wins and stops timeout", () => {
            var t=Target(5); var clock=new CombatCountdown(); bool timedOut=false;
            Apply(t.controller,Burn(),t.hp.CurrentUnits);
            clock.OnAdvanced += elapsed=>{ t.controller.Advance(elapsed); if(t.hp.IsDead) clock.Stop(); };
            clock.OnTimeUp += ()=>timedOut=true; clock.Start(5); clock.Advance(5);
            Equal(true,t.hp.IsDead); Equal(false,timedOut); clock.Advance(1); Equal(false,timedOut);
        });
        Check("deadline clamps late frame, living boss times out after due ticks", () => {
            var t=Target(6); var clock=new CombatCountdown(); bool timedOut=false;
            Apply(t.controller,Burn(),t.hp.CurrentUnits); clock.OnAdvanced+=t.controller.Advance;
            clock.OnTimeUp+=()=>{ Equal(5,t.ticks.Count);timedOut=true;}; clock.Start(5);clock.Advance(10);
            Equal(true,timedOut);Equal(1m,t.hp.Current);Equal(5.0,clock.Elapsed);
        });
        Check("countdown stop resume add reset preserve clock contract", () => {
            var clock=new CombatCountdown();clock.Start(5);clock.Advance(1);clock.Stop();clock.Advance(100);Equal(1.0,clock.Elapsed);
            clock.AddTime(2);clock.Resume();clock.Advance(1);Equal(2.0,clock.Elapsed);Equal(5.0,clock.Remaining);
            clock.Start(10);Equal(0.0,clock.Elapsed);Equal(10.0,clock.Remaining);
        });
        Console.WriteLine($"Completed {_passed} debuff checks. Unity scene execution is a separate gate.");
    }
}

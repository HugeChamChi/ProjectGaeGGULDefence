using UnityEngine;

/// <summary>Explicit offline preview of authored Lizard patterns. No login, account writes or production boss-roster changes.</summary>
public sealed class LizardBossLab : MonoBehaviour
{
    [SerializeField] private BossLizard _boss;
    [SerializeField] private BossData _data;
    [SerializeField] private BossPatternController _controller;
    [SerializeField] private GridManager _grid;
    [SerializeField] private DebuffSettings _debuffSettings;
    [SerializeField] private UnitData _previewUnitData;
    [SerializeField] private Sprite _previewUnitSprite;
    [SerializeField] private Camera _previewCamera;
    private bool _paused;
    private GUIStyle _buttonStyle, _labelStyle;

    private void Start()
    {
        var camera = _previewCamera;
        if (camera != null) camera.orthographicSize = Mathf.Max(7.5f,4.25f/camera.aspect);
        _grid.Init();
        _controller.enabled = false;
        _controller.ConfigureScene(_grid);
        _boss.ConfigureDebuffs(null, _debuffSettings, null, 0d, null);
        ResetEncounter();
        int index = 0;
        foreach (var cell in _grid.AllCells())
        {
            if (index++ % 3 != 0) continue;
            var go = new GameObject("Lizard preview unit");
            go.transform.SetParent(cell.transform, false);
            go.transform.localPosition = Vector3.up * .12f;
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = _previewUnitSprite;
            sprite.sortingLayerName = "Unit";
            sprite.sortingOrder = 2;
            go.transform.localScale = Vector3.one * (.9f / Mathf.Max(.01f, sprite.sprite.bounds.size.y));
            var unit = go.AddComponent<LizardPreviewUnit>();
            unit.unitData = _previewUnitData;
            unit.Init(new UnitDependencies { GridManager = _grid });
            cell.TryPlaceUnit(unit);
            unit.OnPlaced(null, _boss, cell);
        }
    }

    private void Update()
    {
        if (!_paused) _controller.AdvanceCombat(Time.deltaTime);
    }

    /// <summary>Restarts HP and one-shot pattern flags through the real registration path.</summary>
    public void ResetEncounter()
    {
        _controller.UnregisterBoss(_boss);
        _boss.Init(_data.MaxHp);
        _controller.RegisterBoss(_boss, _boss.Patterns);
    }

    private void DamageTo(decimal fraction)
    {
        decimal desired = _boss.MaxHp * fraction;
        if (_boss.CurrentHp <= desired || !_boss.IsTargetable) return;
        _boss.TakeDamage((_boss.CurrentHp - desired) / (_boss.BarrierActive ? .8m : 1m));
    }

    private void OnGUI()
    {
        _buttonStyle ??= new GUIStyle(GUI.skin.button) { fontSize = 24 };
        _labelStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 24, alignment = TextAnchor.MiddleCenter };
        float scale = Screen.width / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale,scale,1f));
        float height = Screen.height / scale;
        GUI.Box(new Rect(30,35,1020,125),GUIContent.none);
        GUI.Label(new Rect(40,45,1000,45),"LIZARD  |  " + (_boss.CastingPattern != null ? _boss.CastingPattern.patternName : "Idle"),_labelStyle);
        GUI.Label(new Rect(40,93,1000,45),$"HP {_boss.CurrentHp:0} / {_boss.MaxHp:0}   |   Slow {_boss.ThreatRemaining:0.0}s",_labelStyle);
        float y=height-185f;
        if(GUI.Button(new Rect(30,y,195,65),"+20s",_buttonStyle)) _controller.AdvanceCombat(20f);
        if(GUI.Button(new Rect(235,y,195,65),"HP 70%",_buttonStyle)) DamageTo(.7m);
        if(GUI.Button(new Rect(440,y,195,65),"HP 30%",_buttonStyle)) DamageTo(.3m);
        if(GUI.Button(new Rect(645,y,195,65),_paused?"Resume":"Hold",_buttonStyle)) _paused=!_paused;
        if(GUI.Button(new Rect(850,y,195,65),"Reset",_buttonStyle)) ResetEncounter();
        GUI.Label(new Rect(30,y+75,1020,45),"Offline pattern preview - production balance and account state are not used",_labelStyle);
    }
}

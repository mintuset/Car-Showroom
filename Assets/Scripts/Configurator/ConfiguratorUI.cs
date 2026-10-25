using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

/// <summary>
/// Runtime UI Toolkit panel for the GLE configurator: trim selection with an equipment
/// table, paint, wheels, upholstery, hood / sunroof controls and camera viewpoints.
/// The whole tree is built in code from <see cref="GleCatalog"/>; styling lives in Configurator.uss.
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class ConfiguratorUI : MonoBehaviour
{
    [Serializable]
    public class Viewpoint
    {
        public string label;
        [Tooltip("Camera eye position and look direction.")]
        public Transform point;
        [FormerlySerializedAs("allowWalking")]
        [Tooltip("Shows the whole car from outside. Paint and wheel changes made from other views jump to one of these.")]
        public bool wholeCar;
        [Tooltip("Vertical field of view for this view. 0 uses the camera's default for inside or outside the car.")]
        public float fieldOfView;
    }

    private enum Tab { Model, Exterior, Interior, Views }

    [Header("References")]
    [SerializeField] private CarConfigurator car;
    [SerializeField] private ShowroomCamera cameraRig;
    [SerializeField] private StyleSheet styleSheet;

    [Header("Viewpoints")]
    [SerializeField] private Viewpoint frontView;
    [SerializeField] private Viewpoint rearView;
    [SerializeField] private Viewpoint sideView;
    [SerializeField] private Viewpoint engineBayView;
    [SerializeField] private Viewpoint driverSeatView;
    [SerializeField] private Viewpoint rearSeatView;
    [SerializeField] private Viewpoint sunroofView;

    [Header("Transitions")]
    [SerializeField, Tooltip("Exterior-to-exterior moves orbit around this point so the camera never passes through the car.")]
    private Transform orbitPivot;
    [SerializeField, Tooltip("Glide time for a half-turn around the car; shorter moves scale down from this.")]
    private float glideDuration = 1.4f;
    [SerializeField] private float fadeDuration = 0.3f;

    [Header("Defaults")]
    [SerializeField] private int startVariant = 1;

    private int variantIndex;
    private int paintIndex;
    private int upholsteryIndex;
    private Tab tab = Tab.Model;
    private Viewpoint currentView;
    private bool includedCollapsed;
    private Coroutine transition;
    private float fadeAlpha = 1f;

    private VisualElement root;
    private VisualElement fade;
    private VisualElement includedPanel;
    private Label variantSubtitle;
    private Label summaryLabel;
    private ScrollView content;
    private VisualElement overlay;
    private readonly Button[] tabButtons = new Button[4];

    private GleCatalog.Variant Variant => GleCatalog.Variants[variantIndex];
    private bool InsideCar => IsInterior(currentView);
    private bool AtFixedView => currentView != null && !currentView.wholeCar;

    private void OnEnable()
    {
        root = GetComponent<UIDocument>().rootVisualElement;
        if (styleSheet != null) root.styleSheets.Add(styleSheet);

        BuildShell();

        if (cameraRig != null)
        {
            cameraRig.OrbitPivot = orbitPivot;
            cameraRig.IsPointerBlocked = IsPointerOverUI;
        }
    }

    private void Start()
    {
        // After every Awake, so CarConfigurator has created its runtime materials.
        SelectVariant(Mathf.Clamp(startVariant, 0, GleCatalog.Variants.Length - 1));
        GoTo(frontView); // First move is a cut that fades in from black.
    }

    // ---------------------------------------------------------------------
    // State changes
    // ---------------------------------------------------------------------
    private void SelectVariant(int index)
    {
        string previousPaint = paintIndex < Variant.Paints.Length ? Variant.Paints[paintIndex].Name : null;
        string previousSeat = upholsteryIndex < Variant.Upholstery.Length ? Variant.Upholstery[upholsteryIndex].Name : null;

        variantIndex = index;
        paintIndex = Mathf.Max(0, Array.FindIndex(Variant.Paints, p => p.Name == previousPaint));
        upholsteryIndex = Mathf.Max(0, Array.FindIndex(Variant.Upholstery, u => u.Name == previousSeat));

        car.SetPaint(Variant.Paints[paintIndex]);
        car.SetUpholstery(Variant.Upholstery[upholsteryIndex]);
        car.SetWheels(Variant.DefaultWheel);

        variantSubtitle.text = $"{GleCatalog.ModelYear} {Variant.Name}";
        Refresh();
    }

    private void SelectPaint(int index)
    {
        paintIndex = index;
        car.SetPaint(Variant.Paints[index]);
        if (InsideCar || AtFixedView) GoTo(frontView);
        Refresh();
    }

    private void SelectWheels(int index)
    {
        car.SetWheels(index);
        if (InsideCar || AtFixedView) GoTo(sideView);
        Refresh();
    }

    private void SelectUpholstery(int index)
    {
        upholsteryIndex = index;
        car.SetUpholstery(Variant.Upholstery[index]);
        if (!InsideCar) GoTo(rearSeatView);
        Refresh();
    }

    private void ToggleHood()
    {
        bool open = !car.HoodOpen;
        car.SetHoodOpen(open);
        if (open) GoTo(engineBayView);
        Refresh();
    }

    private void ToggleSunroof()
    {
        bool open = !car.SunroofOpen;
        car.SetSunroofOpen(open);
        if (open && !InsideCar) GoTo(sunroofView);
        Refresh();
    }

    private void GoTo(Viewpoint view)
    {
        if (view == null || view.point == null || cameraRig == null) return;

        Viewpoint from = currentView;
        currentView = view;

        // Moving into, out of or around the cabin would clip through the body, so those cut
        // behind a fade; everything outside glides around the car.
        IEnumerator move = from == null ? CutTo(view)
            : IsInterior(from) || IsInterior(view) ? FadeTo(view)
            : GlideTo(view);

        if (transition != null) StopCoroutine(transition);
        cameraRig.InputLocked = true;
        transition = StartCoroutine(move);
        Refresh();
    }

    // ---------------------------------------------------------------------
    // Camera transitions
    // ---------------------------------------------------------------------
    private bool IsInterior(Viewpoint view) => view != null && (view == driverSeatView || view == rearSeatView);

    private static void GetPose(Viewpoint view, out Vector3 eye, out float yaw, out float pitch)
    {
        Vector3 euler = view.point.eulerAngles;
        eye = view.point.position;
        yaw = euler.y;
        pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    private void Arrive(Viewpoint view)
    {
        GetPose(view, out Vector3 eye, out float yaw, out float pitch);
        cameraRig.Place(eye, yaw, pitch, IsInterior(view) ? ShowroomCamera.DragMode.Pan : ShowroomCamera.DragMode.Orbit, view.fieldOfView);
        car.SetCabinLight(IsInterior(view));
    }

    private void EndTransition()
    {
        transition = null;
        cameraRig.InputLocked = false;
    }

    private IEnumerator CutTo(Viewpoint view)
    {
        Arrive(view);
        yield return FadeAlpha(0f);
        EndTransition();
    }

    private IEnumerator FadeTo(Viewpoint view)
    {
        yield return FadeAlpha(1f);
        Arrive(view);
        yield return null; // let the new view render once before revealing it
        yield return FadeAlpha(0f);
        EndTransition();
    }

    private IEnumerator GlideTo(Viewpoint view)
    {
        if (fadeAlpha > 0f) yield return FadeAlpha(0f);

        GetPose(view, out Vector3 endEye, out float endYaw, out float endPitch);
        Vector3 startEye = cameraRig.EyePosition;
        float startYaw = cameraRig.Yaw;
        float startPitch = cameraRig.Pitch;
        float startFov = cameraRig.FieldOfView;
        float endFov = cameraRig.ResolveFieldOfView(view.fieldOfView, ShowroomCamera.DragMode.Orbit);
        car.SetCabinLight(false);

        // Interpolate in cylindrical coordinates around the car: angle, radius and height.
        Vector3 pivot = orbitPivot != null ? orbitPivot.position : Vector3.zero;
        Vector2 from = new Vector2(startEye.x - pivot.x, startEye.z - pivot.z);
        Vector2 to = new Vector2(endEye.x - pivot.x, endEye.z - pivot.z);
        float fromAngle = Mathf.Atan2(from.y, from.x) * Mathf.Rad2Deg;
        float toAngle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        float turn = Mathf.Abs(Mathf.DeltaAngle(fromAngle, toAngle));
        float duration = glideDuration * Mathf.Lerp(0.45f, 1f, turn / 180f);

        for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
        {
            float e = EaseInOutCubic(t);
            float angle = Mathf.LerpAngle(fromAngle, toAngle, e) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(from.magnitude, to.magnitude, e);
            var eye = new Vector3(
                pivot.x + Mathf.Cos(angle) * radius,
                Mathf.Lerp(startEye.y, endEye.y, e),
                pivot.z + Mathf.Sin(angle) * radius);
            cameraRig.Place(eye, Mathf.LerpAngle(startYaw, endYaw, e), Mathf.Lerp(startPitch, endPitch, e), ShowroomCamera.DragMode.Orbit,
                Mathf.Lerp(startFov, endFov, e));
            yield return null;
        }

        Arrive(view);
        EndTransition();
    }

    private IEnumerator FadeAlpha(float target)
    {
        float speed = 1f / Mathf.Max(0.01f, fadeDuration);
        while (!Mathf.Approximately(fadeAlpha, target))
        {
            fadeAlpha = Mathf.MoveTowards(fadeAlpha, target, Time.deltaTime * speed);
            fade.style.opacity = fadeAlpha;
            yield return null;
        }
    }

    private static float EaseInOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }

    // ---------------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------------
    private void BuildShell()
    {
        root.Clear();
        root.AddToClassList("cfg-root");

        // Behind the panels: only the 3D view fades during cuts.
        fade = new VisualElement();
        fade.AddToClassList("cfg-fade");
        fade.pickingMode = PickingMode.Ignore;
        fade.style.opacity = fadeAlpha;
        root.Add(fade);

        var panel = new VisualElement();
        panel.AddToClassList("cfg-panel");
        root.Add(panel);

        var header = new VisualElement();
        header.AddToClassList("cfg-header");
        header.Add(MakeLabel("MERCEDES-BENZ", "cfg-brand"));
        header.Add(MakeLabel("GLE SUV", "cfg-title"));
        variantSubtitle = MakeLabel("", "cfg-subtitle");
        header.Add(variantSubtitle);
        panel.Add(header);

        var tabs = new VisualElement();
        tabs.AddToClassList("cfg-tabs");
        string[] tabNames = { "Model", "Exterior", "Interior", "Views" };
        for (int i = 0; i < tabNames.Length; i++)
        {
            var t = (Tab)i;
            var button = new Button(() => { tab = t; Refresh(); }) { text = tabNames[i] };
            button.AddToClassList("cfg-tab");
            tabButtons[i] = button;
            tabs.Add(button);
        }
        panel.Add(tabs);

        content = new ScrollView(ScrollViewMode.Vertical);
        content.AddToClassList("cfg-content");
        panel.Add(content);

        summaryLabel = MakeLabel("", "cfg-summary");
        panel.Add(summaryLabel);

        includedPanel = new VisualElement();
        includedPanel.AddToClassList("cfg-panel");
        includedPanel.AddToClassList("cfg-side");
        root.Add(includedPanel);

        var hintBar = new VisualElement();
        hintBar.AddToClassList("cfg-hint-bar");
        hintBar.pickingMode = PickingMode.Ignore;
        var hint = MakeLabel("Drag to orbit the car  ·  Drag inside the car to look around", "cfg-hint");
        hint.pickingMode = PickingMode.Ignore;
        hintBar.Add(hint);
        root.Add(hintBar);

        overlay = new VisualElement();
        overlay.AddToClassList("cfg-overlay");
        overlay.style.display = DisplayStyle.None;
        overlay.RegisterCallback<ClickEvent>(e => { if (e.target == overlay) ShowCompare(false); });
        root.Add(overlay);
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (root?.panel == null) return false;
        Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
        VisualElement picked = root.panel.Pick(panelPosition);
        return picked != null && picked != root && root.Contains(picked);
    }

    private void Refresh()
    {
        if (content == null) return;

        for (int i = 0; i < tabButtons.Length; i++)
            tabButtons[i].EnableInClassList("cfg-tab--active", (int)tab == i);

        float scroll = content.scrollOffset.y;
        content.Clear();
        switch (tab)
        {
            case Tab.Model: BuildModelTab(); break;
            case Tab.Exterior: BuildExteriorTab(); break;
            case Tab.Interior: BuildInteriorTab(); break;
            case Tab.Views: BuildViewsTab(); break;
        }
        content.schedule.Execute(() => content.scrollOffset = new Vector2(0f, scroll));
        BuildIncludedPanel();

        summaryLabel.text =
            $"{Variant.Name}  ·  {Variant.Paints[paintIndex].Name}\n" +
            $"{GleCatalog.Wheels[car.WheelIndex].Name}  ·  {Variant.Upholstery[upholsteryIndex].DisplayName}";
    }

    private void BuildModelTab()
    {
        var section = Section("Select a model");
        for (int i = 0; i < GleCatalog.Variants.Length; i++)
        {
            int index = i;
            GleCatalog.Variant v = GleCatalog.Variants[i];
            var card = new Button(() => SelectVariant(index));
            card.AddToClassList("cfg-card");
            card.EnableInClassList("cfg-card--selected", i == variantIndex);
            card.Add(MakeLabel(v.Name, "cfg-card__title"));
            card.Add(MakeLabel($"{v.Output}  ·  0-60 mph {v.ZeroToSixty}", "cfg-card__meta"));
            card.Add(MakeLabel(v.Powertrain, "cfg-card__detail"));
            section.Add(card);
        }
    }

    private void BuildIncludedPanel()
    {
        includedPanel.Clear();

        var top = new VisualElement();
        top.AddToClassList("cfg-side__top");
        var titles = new VisualElement();
        titles.Add(MakeLabel("What's included", "cfg-section__title"));
        titles.Add(MakeLabel(Variant.Name, "cfg-option-name"));
        top.Add(titles);
        var toggle = new Button(() => { includedCollapsed = !includedCollapsed; BuildIncludedPanel(); })
        {
            text = includedCollapsed ? "Show" : "Hide",
        };
        toggle.AddToClassList("cfg-action");
        toggle.AddToClassList("cfg-action--small");
        top.Add(toggle);
        includedPanel.Add(top);

        if (includedCollapsed) return;

        includedPanel.Add(MakeLegend());
        for (int f = 0; f < GleCatalog.Features.Length; f++)
        {
            var row = new VisualElement();
            row.AddToClassList("cfg-feature");
            row.Add(new StatusIcon(Variant.Equipment[f]));
            var name = MakeLabel(GleCatalog.Features[f], "cfg-feature__name");
            name.EnableInClassList("cfg-feature__name--missing", Variant.Equipment[f] == GleCatalog.Availability.NotAvailable);
            row.Add(name);
            includedPanel.Add(row);
        }

        var compare = new Button(() => ShowCompare(true)) { text = "Compare all models" };
        compare.AddToClassList("cfg-action");
        compare.AddToClassList("cfg-side__compare");
        includedPanel.Add(compare);
        includedPanel.Add(MakeLabel(GleCatalog.SourceNote, "cfg-note"));
    }

    private void BuildExteriorTab()
    {
        GleCatalog.PaintOption[] paints = Variant.Paints;
        var paint = Section("Paint");
        var paintName = MakeLabel(paints[paintIndex].Name, "cfg-option-name");
        paint.Add(paintName);
        paint.Add(MakeLabel("Standard & metallic", "cfg-group"));
        paint.Add(SwatchGrid(paints, p => !p.Manufaktur, paintName));
        if (Array.Exists(paints, p => p.Manufaktur))
        {
            paint.Add(MakeLabel("MANUFAKTUR", "cfg-group"));
            paint.Add(SwatchGrid(paints, p => p.Manufaktur, paintName));
        }

        var wheels = Section("Wheels");
        for (int i = 0; i < GleCatalog.Wheels.Length; i++)
        {
            int index = i;
            GleCatalog.WheelOption w = GleCatalog.Wheels[i];
            var row = new Button(() => SelectWheels(index));
            row.AddToClassList("cfg-card");
            row.EnableInClassList("cfg-card--selected", i == car.WheelIndex);
            row.Add(MakeLabel(w.Name + (i == Variant.DefaultWheel ? "   (standard)" : ""), "cfg-card__title"));
            row.Add(MakeLabel(w.Description, "cfg-card__detail"));
            wheels.Add(row);
        }

        var hood = Section("Engine hood");
        hood.Add(MakeLabel($"{Variant.Powertrain}, {Variant.Output}", "cfg-option-name"));
        hood.Add(ActionButton(car.HoodOpen ? "Close hood" : "Open hood & view engine bay", ToggleHood, car.HoodOpen));

        var roof = Section("Panorama sunroof");
        roof.Add(ActionButton(car.SunroofOpen ? "Close sunroof" : "Open sunroof", ToggleSunroof, car.SunroofOpen));
    }

    private void BuildInteriorTab()
    {
        GleCatalog.UpholsteryOption[] seats = Variant.Upholstery;
        var upholstery = Section("Seat upholstery");
        var seatName = MakeLabel(seats[upholsteryIndex].DisplayName, "cfg-option-name");
        upholstery.Add(seatName);

        foreach (string material in new[] { "MB-Tex", "Leather", "Nappa Leather" })
        {
            if (!Array.Exists(seats, s => s.Material == material && !s.Manufaktur)) continue;
            upholstery.Add(MakeLabel(material, "cfg-group"));
            upholstery.Add(SwatchGrid(seats, s => s.Material == material && !s.Manufaktur, seatName));
        }
        if (Array.Exists(seats, s => s.Manufaktur))
        {
            upholstery.Add(MakeLabel("MANUFAKTUR", "cfg-group"));
            upholstery.Add(SwatchGrid(seats, s => s.Manufaktur, seatName));
        }

        var views = Section("Interior views");
        views.Add(ActionButton("Driver seat", () => GoTo(driverSeatView), currentView == driverSeatView));
        views.Add(ActionButton("Rear seat", () => GoTo(rearSeatView), currentView == rearSeatView));

        var roof = Section("Panorama sunroof");
        roof.Add(ActionButton(car.SunroofOpen ? "Close sunroof" : "Open sunroof", ToggleSunroof, car.SunroofOpen));
    }

    private void BuildViewsTab()
    {
        var exterior = Section("Exterior");
        exterior.Add(ViewButton(frontView));
        exterior.Add(ViewButton(rearView));
        exterior.Add(ViewButton(sideView));
        exterior.Add(ViewButton(engineBayView));
        exterior.Add(ViewButton(sunroofView));

        var interior = Section("Interior");
        interior.Add(ViewButton(driverSeatView));
        interior.Add(ViewButton(rearSeatView));
    }

    private void ShowCompare(bool show)
    {
        overlay.Clear();
        overlay.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
        if (!show) return;

        var dialog = new VisualElement();
        dialog.AddToClassList("cfg-dialog");
        overlay.Add(dialog);

        var top = new VisualElement();
        top.AddToClassList("cfg-dialog__top");
        top.Add(MakeLabel($"{GleCatalog.ModelYear} {GleCatalog.ModelName}: compare models", "cfg-section__title"));
        var close = new Button(() => ShowCompare(false)) { text = "Close" };
        close.AddToClassList("cfg-action");
        close.AddToClassList("cfg-action--small");
        top.Add(close);
        dialog.Add(top);
        dialog.Add(MakeLegend());

        var table = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
        table.AddToClassList("cfg-table");
        dialog.Add(table);

        var head = TableRow(true);
        head.Add(TableCell("", true, true));
        for (int v = 0; v < GleCatalog.Variants.Length; v++)
        {
            int index = v;
            var cell = new Button(() => { SelectVariant(index); ShowCompare(true); }) { text = GleCatalog.Variants[v].Name };
            cell.AddToClassList("cfg-cell");
            cell.AddToClassList("cfg-cell--head");
            cell.EnableInClassList("cfg-cell--selected", v == variantIndex);
            head.Add(cell);
        }
        table.Add(head);

        AddTextRow(table, "Output", v => v.Output);
        AddTextRow(table, "0-60 mph", v => v.ZeroToSixty);
        for (int f = 0; f < GleCatalog.Features.Length; f++)
        {
            var row = TableRow(false);
            row.Add(TableCell(GleCatalog.Features[f], true, false));
            for (int v = 0; v < GleCatalog.Variants.Length; v++)
            {
                var cell = TableCell("", false, false);
                cell.EnableInClassList("cfg-cell--selected", v == variantIndex);
                cell.Add(new StatusIcon(GleCatalog.Variants[v].Equipment[f]));
                row.Add(cell);
            }
            table.Add(row);
        }

        dialog.Add(MakeLabel("Click a model name to select it. " + GleCatalog.SourceNote, "cfg-note"));
    }

    private void AddTextRow(ScrollView table, string title, Func<GleCatalog.Variant, string> value)
    {
        var row = TableRow(false);
        row.Add(TableCell(title, true, false));
        for (int v = 0; v < GleCatalog.Variants.Length; v++)
        {
            var cell = TableCell(value(GleCatalog.Variants[v]), false, false);
            cell.EnableInClassList("cfg-cell--selected", v == variantIndex);
            row.Add(cell);
        }
        table.Add(row);
    }

    // ---------------------------------------------------------------------
    // Element helpers
    // ---------------------------------------------------------------------
    private VisualElement Section(string title)
    {
        var section = new VisualElement();
        section.AddToClassList("cfg-section");
        section.Add(MakeLabel(title, "cfg-section__title"));
        content.Add(section);
        return section;
    }

    private VisualElement SwatchGrid(GleCatalog.PaintOption[] options, Predicate<GleCatalog.PaintOption> filter, Label nameLabel)
    {
        var grid = new VisualElement();
        grid.AddToClassList("cfg-swatches");
        for (int i = 0; i < options.Length; i++)
        {
            if (!filter(options[i])) continue;
            int index = i;
            grid.Add(Swatch(options[i].Color, options[i].Name, i == paintIndex, () => SelectPaint(index), nameLabel,
                () => Variant.Paints[paintIndex].Name));
        }
        return grid;
    }

    private VisualElement SwatchGrid(GleCatalog.UpholsteryOption[] options, Predicate<GleCatalog.UpholsteryOption> filter, Label nameLabel)
    {
        var grid = new VisualElement();
        grid.AddToClassList("cfg-swatches");
        for (int i = 0; i < options.Length; i++)
        {
            if (!filter(options[i])) continue;
            int index = i;
            grid.Add(Swatch(options[i].Color, options[i].DisplayName, i == upholsteryIndex, () => SelectUpholstery(index), nameLabel,
                () => Variant.Upholstery[upholsteryIndex].DisplayName));
        }
        return grid;
    }

    private static VisualElement Swatch(Color color, string name, bool selected, Action onClick, Label nameLabel, Func<string> currentName)
    {
        var swatch = new Button(onClick);
        swatch.AddToClassList("cfg-swatch");
        swatch.EnableInClassList("cfg-swatch--selected", selected);
        var chip = new VisualElement();
        chip.AddToClassList("cfg-swatch__chip");
        chip.style.backgroundColor = color;
        swatch.Add(chip);
        swatch.RegisterCallback<PointerEnterEvent>(_ => nameLabel.text = name);
        swatch.RegisterCallback<PointerLeaveEvent>(_ => nameLabel.text = currentName());
        return swatch;
    }

    private Button ViewButton(Viewpoint view)
    {
        string label = view != null && !string.IsNullOrEmpty(view.label) ? view.label : "View";
        return ActionButton(label, () => GoTo(view), currentView == view);
    }

    private static Button ActionButton(string text, Action onClick, bool active = false)
    {
        var button = new Button(onClick) { text = text };
        button.AddToClassList("cfg-action");
        button.EnableInClassList("cfg-action--active", active);
        return button;
    }

    private static VisualElement MakeLegend()
    {
        var legend = new VisualElement();
        legend.AddToClassList("cfg-legend");
        AddLegendItem(legend, GleCatalog.Availability.Standard, "Standard");
        AddLegendItem(legend, GleCatalog.Availability.Optional, "Optional (extra cost)");
        AddLegendItem(legend, GleCatalog.Availability.NotAvailable, "Not available");
        return legend;
    }

    private static void AddLegendItem(VisualElement legend, GleCatalog.Availability availability, string text)
    {
        legend.Add(new StatusIcon(availability));
        legend.Add(MakeLabel(text, "cfg-legend__text"));
    }

    private static VisualElement TableRow(bool head)
    {
        var row = new VisualElement();
        row.AddToClassList("cfg-row");
        row.EnableInClassList("cfg-row--head", head);
        return row;
    }

    private static VisualElement TableCell(string text, bool first, bool head)
    {
        var cell = new VisualElement();
        cell.AddToClassList("cfg-cell");
        cell.EnableInClassList("cfg-cell--first", first);
        cell.EnableInClassList("cfg-cell--head", head);
        if (!string.IsNullOrEmpty(text)) cell.Add(MakeLabel(text, "cfg-cell__text"));
        return cell;
    }

    private static Label MakeLabel(string text, string className)
    {
        var label = new Label(text);
        label.AddToClassList(className);
        return label;
    }

    /// <summary>Vector tick / plus / dash, drawn with Painter2D so it does not depend on font glyphs.</summary>
    private class StatusIcon : VisualElement
    {
        private readonly GleCatalog.Availability availability;

        public StatusIcon(GleCatalog.Availability availability)
        {
            this.availability = availability;
            AddToClassList("cfg-icon");
            AddToClassList(availability switch
            {
                GleCatalog.Availability.Standard => "cfg-icon--standard",
                GleCatalog.Availability.Optional => "cfg-icon--optional",
                _ => "cfg-icon--none",
            });
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            Painter2D p = ctx.painter2D;
            p.lineWidth = Mathf.Max(1.5f, r.width * 0.14f);
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            p.strokeColor = resolvedStyle.color;

            p.BeginPath();
            switch (availability)
            {
                case GleCatalog.Availability.Standard:
                    p.MoveTo(new Vector2(r.xMin + r.width * 0.22f, r.yMin + r.height * 0.52f));
                    p.LineTo(new Vector2(r.xMin + r.width * 0.42f, r.yMin + r.height * 0.72f));
                    p.LineTo(new Vector2(r.xMin + r.width * 0.78f, r.yMin + r.height * 0.30f));
                    break;
                case GleCatalog.Availability.Optional:
                    p.MoveTo(new Vector2(r.center.x, r.yMin + r.height * 0.25f));
                    p.LineTo(new Vector2(r.center.x, r.yMax - r.height * 0.25f));
                    p.MoveTo(new Vector2(r.xMin + r.width * 0.25f, r.center.y));
                    p.LineTo(new Vector2(r.xMax - r.width * 0.25f, r.center.y));
                    break;
                default:
                    p.MoveTo(new Vector2(r.xMin + r.width * 0.3f, r.center.y));
                    p.LineTo(new Vector2(r.xMax - r.width * 0.3f, r.center.y));
                    break;
            }
            p.Stroke();
        }
    }
}

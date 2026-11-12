using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.Services;

/// <summary>Defaults and validation for the PC -> phone control mapping.</summary>
public static class MobileInputCatalog
{
    public const string Joystick = "joystick";
    public const string FullscreenAction = "fullscreen";
    public const string TouchToggleAction = "touchmode";

    public static IReadOnlyList<InputActionInfo> Actions { get; } =
    [
        new() { Id = "forward", Label = "Avanzar", Description = "W · movimiento hacia delante", DefaultVirtualKey = 0x57, IsMovement = true, TouchTargetId = Joystick, Glyph = "\uE74A" },
        new() { Id = "back", Label = "Retroceder", Description = "S · movimiento hacia atrás", DefaultVirtualKey = 0x53, IsMovement = true, TouchTargetId = Joystick, Glyph = "\uE74B" },
        new() { Id = "left", Label = "Izquierda", Description = "A · movimiento lateral", DefaultVirtualKey = 0x41, IsMovement = true, TouchTargetId = Joystick, Glyph = "\uE72B" },
        new() { Id = "right", Label = "Derecha", Description = "D · movimiento lateral", DefaultVirtualKey = 0x44, IsMovement = true, TouchTargetId = Joystick, Glyph = "\uE72A" },
        new() { Id = "jump", Label = "Saltar", Description = "Espacio · salto", DefaultVirtualKey = 0x20, TouchTargetId = "jump", Glyph = "\uE74A" },
        new() { Id = "sneak", Label = "Agacharse", Description = "Shift · sigilo (alterna)", DefaultVirtualKey = 0xA0, TouchTargetId = "sneak", Glyph = "\uE74C" },
        new() { Id = "inventory", Label = "Inventario", Description = "E · abrir inventario", DefaultVirtualKey = 0x45, TouchTargetId = "inventory", Glyph = "\uE7B8" },
        new() { Id = "pause", Label = "Pausa", Description = "Esc · menú de pausa", DefaultVirtualKey = 0x1B, TouchTargetId = "pause", Glyph = "\uE769" },
        new() { Id = "drop", Label = "Soltar objeto", Description = "Q · soltar el objeto actual", DefaultVirtualKey = 0x51, TouchTargetId = "drop", Glyph = "\uE74D" },
        new() { Id = "hotbar1", Label = "Ranura 1", Description = "1 · primera ranura", DefaultVirtualKey = 0x31, TouchTargetId = "hotbar1", Glyph = "\uE8FD" },
        new() { Id = "hotbar2", Label = "Ranura 2", Description = "2", DefaultVirtualKey = 0x32, TouchTargetId = "hotbar2", Glyph = "\uE8FD" },
        new() { Id = "hotbar3", Label = "Ranura 3", Description = "3", DefaultVirtualKey = 0x33, TouchTargetId = "hotbar3", Glyph = "\uE8FD" },
        new() { Id = "hotbar4", Label = "Ranura 4", Description = "4", DefaultVirtualKey = 0x34, TouchTargetId = "hotbar4", Glyph = "\uE8FD" },
        new() { Id = "hotbar5", Label = "Ranura 5", Description = "5", DefaultVirtualKey = 0x35, TouchTargetId = "hotbar5", Glyph = "\uE8FD" },
        new() { Id = "hotbar6", Label = "Ranura 6", Description = "6", DefaultVirtualKey = 0x36, TouchTargetId = "hotbar6", Glyph = "\uE8FD" },
        new() { Id = "hotbar7", Label = "Ranura 7", Description = "7", DefaultVirtualKey = 0x37, TouchTargetId = "hotbar7", Glyph = "\uE8FD" },
        new() { Id = "hotbar8", Label = "Ranura 8", Description = "8", DefaultVirtualKey = 0x38, TouchTargetId = "hotbar8", Glyph = "\uE8FD" },
        new() { Id = "hotbar9", Label = "Ranura 9", Description = "9", DefaultVirtualKey = 0x39, TouchTargetId = "hotbar9", Glyph = "\uE8FD" },
        new() { Id = "sprint", Label = "Correr", Description = "Ctrl · esprintar", DefaultVirtualKey = 0xA2, Glyph = "\uE805" },
        new() { Id = "attack", Label = "Atacar / romper", Description = "Clic izquierdo · golpear y romper bloques", DefaultVirtualKey = 0xA1, TouchTargetId = "attack", Glyph = "\uE72A" },
        new() { Id = "place", Label = "Colocar / usar", Description = "Clic derecho · colocar bloques y usar objetos", DefaultVirtualKey = 0xA2, TouchTargetId = "place", Glyph = "\uE71D" },
        new() { Id = "chat", Label = "Chat", Description = "T · abrir chat", DefaultVirtualKey = 0x54, TouchTargetId = "chat", Glyph = "\uE8BD" },
        new() { Id = "perspective", Label = "Cambiar cámara", Description = "F5 · primera/tercera persona", DefaultVirtualKey = 0x74, Glyph = "\uE7C4" },
        new() { Id = FullscreenAction, Label = "Pantalla completa", Description = "F11 · alternar pantalla completa del launcher", DefaultVirtualKey = 0x7A, Glyph = "\uE740" },
        new() { Id = TouchToggleAction, Label = "Modo táctil", Description = "F10 · alternar controles táctiles/nativos", DefaultVirtualKey = 0x79, Glyph = "\uE815" }
    ];

    public static IReadOnlyList<TouchTargetInfo> TouchTargets { get; } =
    [
        new() { Id = Joystick, Label = "Joystick de movimiento", Description = "Centro del joystick táctil. WASD tira del joystick en la dirección indicada.", IsJoystick = true, Glyph = "\uE7C9" },
        new() { Id = "jump", Label = "Botón Saltar", Description = "Botón táctil de salto.", Glyph = "\uE74A" },
        new() { Id = "sneak", Label = "Botón Agacharse", Description = "Botón táctil de sigilo (se alterna).", Glyph = "\uE74C" },
        new() { Id = "inventory", Label = "Botón Inventario", Description = "Botón táctil que abre el inventario.", Glyph = "\uE7B8" },
        new() { Id = "pause", Label = "Botón Pausa", Description = "Botón táctil del menú de pausa.", Glyph = "\uE769" },
        new() { Id = "drop", Label = "Botón Soltar", Description = "Botón táctil para soltar el objeto.", Glyph = "\uE74D" },
        new() { Id = "chat", Label = "Botón Chat", Description = "Botón táctil del chat.", Glyph = "\uE8BD" },
        new() { Id = "attack", Label = "Botón Atacar / Romper", Description = "Botón táctil principal: golpear y romper bloques.", Glyph = "\uE72A" },
        new() { Id = "place", Label = "Botón Colocar / Usar", Description = "Botón táctil secundario: colocar bloques y usar objetos.", Glyph = "\uE71D" },
        new() { Id = "hotbar1", Label = "Ranura 1", Description = "Primera ranura de la barra rápida.", Glyph = "\uE8FD" },
        new() { Id = "hotbar2", Label = "Ranura 2", Description = "Segunda ranura.", Glyph = "\uE8FD" },
        new() { Id = "hotbar3", Label = "Ranura 3", Description = "Tercera ranura.", Glyph = "\uE8FD" },
        new() { Id = "hotbar4", Label = "Ranura 4", Description = "Cuarta ranura.", Glyph = "\uE8FD" },
        new() { Id = "hotbar5", Label = "Ranura 5", Description = "Quinta ranura.", Glyph = "\uE8FD" },
        new() { Id = "hotbar6", Label = "Ranura 6", Description = "Sexta ranura.", Glyph = "\uE8FD" },
        new() { Id = "hotbar7", Label = "Ranura 7", Description = "Séptima ranura.", Glyph = "\uE8FD" },
        new() { Id = "hotbar8", Label = "Ranura 8", Description = "Octava ranura.", Glyph = "\uE8FD" },
        new() { Id = "hotbar9", Label = "Ranura 9", Description = "Novena ranura.", Glyph = "\uE8FD" }
    ];

    public static InputActionInfo? FindAction(string id) =>
        Actions.FirstOrDefault(a => a.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static TouchTargetInfo? FindTarget(string id) =>
        TouchTargets.FirstOrDefault(t => t.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static Dictionary<string, int> BuildDefaultBindings() =>
        Actions.ToDictionary(a => a.Id, a => a.DefaultVirtualKey, StringComparer.OrdinalIgnoreCase);

    public static Dictionary<string, int> GetBindings(MobileConnectSettings settings)
    {
        var defaults = BuildDefaultBindings();
        foreach (var pair in settings.KeyBindings)
        {
            if (defaults.ContainsKey(pair.Key) && pair.Value is > 0 and < 0x100)
                defaults[pair.Key] = pair.Value;
        }
        return defaults;
    }

    public static List<TouchTargetInfo> GetTouchTargets(MobileConnectSettings settings)
    {
        var list = TouchTargets.Select(t => new TouchTargetInfo
        {
            Id = t.Id,
            Label = t.Label,
            Description = t.Description,
            DefaultX = t.DefaultX,
            DefaultY = t.DefaultY,
            IsJoystick = t.IsJoystick,
            Glyph = t.Glyph,
            X = t.DefaultX,
            Y = t.DefaultY
        }).ToList();

        foreach (var target in list)
        {
            if (settings.TouchTargets.TryGetValue(target.Id, out var saved))
            {
                target.X = Math.Clamp(saved.X, 0, 1);
                target.Y = Math.Clamp(saved.Y, 0, 1);
            }
        }
        return list;
    }

    public static string KeyLabel(int virtualKey)
    {
        if (virtualKey <= 0)
            return "Sin asignar";
        try
        {
            var key = System.Windows.Input.KeyInterop.KeyFromVirtualKey(virtualKey);
            return key switch
            {
                System.Windows.Input.Key.LeftShift => "Shift izquierdo",
                System.Windows.Input.Key.RightShift => "Shift derecho",
                System.Windows.Input.Key.LeftCtrl => "Ctrl izquierdo",
                System.Windows.Input.Key.RightCtrl => "Ctrl derecho",
                System.Windows.Input.Key.Escape => "Esc",
                System.Windows.Input.Key.Space => "Espacio",
                System.Windows.Input.Key.Return => "Enter",
                System.Windows.Input.Key.Back => "Retroceso",
                System.Windows.Input.Key.Tab => "Tab",
                System.Windows.Input.Key.OemPlus => "+",
                System.Windows.Input.Key.OemMinus => "-",
                System.Windows.Input.Key.OemComma => ",",
                System.Windows.Input.Key.OemPeriod => ".",
                _ => key.ToString()
            };
        }
        catch
        {
            return $"VK {virtualKey:X2}";
        }
    }
}

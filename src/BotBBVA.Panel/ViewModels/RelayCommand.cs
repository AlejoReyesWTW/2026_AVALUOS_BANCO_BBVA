using System.Windows.Input;

namespace BotBBVA.Panel.ViewModels;

/// <summary>
/// Implementación de ICommand para el patrón MVVM, con y sin parámetro.
/// </summary>
public sealed class RelayCommand : ICommand
{
    private readonly Action<object?> _ejecutar;
    private readonly Func<object?, bool>? _puedeEjecutar;

    public RelayCommand(Action ejecutar, Func<bool>? puedeEjecutar = null)
        : this(_ => ejecutar(), puedeEjecutar is null ? null : _ => puedeEjecutar())
    {
    }

    public RelayCommand(Action<object?> ejecutar, Func<object?, bool>? puedeEjecutar = null)
    {
        _ejecutar = ejecutar ?? throw new ArgumentNullException(nameof(ejecutar));
        _puedeEjecutar = puedeEjecutar;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parametro) => _puedeEjecutar?.Invoke(parametro) ?? true;

    public void Execute(object? parametro) => _ejecutar(parametro);

    /// <summary>
    /// Notifica que el resultado de CanExecute pudo haber cambiado.
    /// </summary>
    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

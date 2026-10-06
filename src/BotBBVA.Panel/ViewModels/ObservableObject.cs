using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace BotBBVA.Panel.ViewModels;

/// <summary>
/// Clase base para los ViewModels: implementa INotifyPropertyChanged.
/// </summary>
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Asigna un valor y notifica el cambio si realmente varió.
    /// </summary>
    protected bool Establecer<T>(ref T campo, T valor, [CallerMemberName] string? nombre = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
        {
            return false;
        }

        campo = valor;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
        return true;
    }

    /// <summary>
    /// Notifica manualmente el cambio de una propiedad calculada.
    /// </summary>
    protected void Notificar([CallerMemberName] string? nombre = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
}

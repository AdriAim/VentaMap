namespace VentaMap.Models;

public enum PublicationStatus
{
    Active,
    PendingPayment,
    DeactivationRequested,
    OwnerDeleted,
    Expired,
    InTrash
}

public static class PublicationStatusExtensions
{
    public static string ToDisplayName(this PublicationStatus status) => status switch
    {
        PublicationStatus.Active => "Activa",
        PublicationStatus.PendingPayment => "Pendiente de pago",
        PublicationStatus.DeactivationRequested => "Baja solicitada",
        PublicationStatus.OwnerDeleted => "Eliminado por usuario",
        PublicationStatus.Expired => "Vencida",
        PublicationStatus.InTrash => "En papelera",
        _ => status.ToString()
    };
}

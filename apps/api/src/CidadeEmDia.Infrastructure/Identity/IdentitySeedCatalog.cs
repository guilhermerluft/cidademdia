using CidadeEmDia.Domain.Identity;

namespace CidadeEmDia.Infrastructure.Identity;

public static class IdentitySeedCatalog
{
    public static IReadOnlyCollection<(string Key, string Description)> Permissions { get; } =
    [
        (IdentityPermissionKeys.ProfileReadSelf, "Ler o próprio perfil privado."),
        (IdentityPermissionKeys.ProfileUpdateSelf, "Atualizar o próprio perfil."),
        (IdentityPermissionKeys.MasterScopeAccess, "Acessar recursos vinculados ao escopo da conta Master."),
        (IdentityPermissionKeys.AdminAccess, "Acessar recursos administrativos protegidos."),
        (SubaccountPermissionKeys.OccurrenceReadTargeted, "Visualizar ocorrências direcionadas à Master quando o vínculo permitir."),
        (SubaccountPermissionKeys.OccurrenceStatusChange, "Alterar status de ocorrência direcionada quando o vínculo e a transição permitirem."),
        (SubaccountPermissionKeys.ChatRead, "Ler chat de ocorrência autorizado no contexto da Master."),
        (SubaccountPermissionKeys.ChatMessageSend, "Enviar mensagem de texto em chat autorizado no contexto da Master."),
        (SubaccountPermissionKeys.ChatAudioSend, "Enviar áudio em chat autorizado no contexto da Master.")
    ];

    public static IReadOnlyCollection<(string Key, string Name, IReadOnlyCollection<string> Permissions)> Roles { get; } =
    [
        (IdentityRoleKeys.Citizen, "Cidadão", [IdentityPermissionKeys.ProfileReadSelf, IdentityPermissionKeys.ProfileUpdateSelf]),
        (IdentityRoleKeys.Master, "Master", [IdentityPermissionKeys.ProfileReadSelf, IdentityPermissionKeys.ProfileUpdateSelf, IdentityPermissionKeys.MasterScopeAccess]),
        (IdentityRoleKeys.Subaccount, "Subconta", [IdentityPermissionKeys.ProfileReadSelf, IdentityPermissionKeys.ProfileUpdateSelf]),
        (IdentityRoleKeys.Admin, "Administrador", IdentityPermissionKeys.All)
    ];
}

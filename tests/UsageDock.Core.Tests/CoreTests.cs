using UsageDock.Core;
namespace UsageDock.Core.Tests;
public class CoreTests
{
    [Fact] public void ImportKnownSchema() => Assert.Equal("token", CredentialImporter.Parse("{\"tokens\":{\"access_token\":\"token\",\"account_id\":\"a\"}}", ProviderKind.Codex).Secret);
    [Fact] public void RejectUnknownImport() => Assert.Throws<InvalidDataException>(() => CredentialImporter.Parse("{\"nested\":{\"access_token\":\"token\"}}", ProviderKind.Codex));
    [Fact] public void RejectEmptyName() => Assert.NotNull(ProfileValidator.Validate(new(Guid.NewGuid(), "", ProviderKind.Codex), "token"));
}

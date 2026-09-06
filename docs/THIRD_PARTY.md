# Third-party components

UsageDock source is licensed under MIT. Its dependencies retain their own licenses.

Self-contained Windows builds redistribute the Microsoft .NET runtime and Windows Desktop runtime. See the [.NET runtime license](https://github.com/dotnet/runtime/blob/main/LICENSE.TXT), [.NET third-party notices](https://github.com/dotnet/runtime/blob/main/THIRD-PARTY-NOTICES.TXT), and [WPF license](https://github.com/dotnet/wpf/blob/main/LICENSE.TXT).

Tests use xUnit, Microsoft.NET.Test.Sdk and coverlet; these are development dependencies and are not included intentionally in the application package. Installer creation uses [Inno Setup](https://jrsoftware.org/isinfo.php), with its own [license](https://github.com/jrsoftware/issrc/blob/main/LICENSE.TXT).

When adding or upgrading a redistributed dependency, retain any notices required by its license and review the package contents before release.

The OpenAI provider icon uses geometry from the [official OpenAI Agents SDK logo asset](https://raw.githubusercontent.com/openai/openai-agents-python/main/docs/assets/logo.svg), obtained on 2026-09-05. It identifies the connected provider; it is not the UsageDock application logo. OpenAI names and logos remain their respective owner's trademarks, separate from UsageDock's MIT license. Their appearance does not imply affiliation or endorsement.

# Dependencies and licensing

Application and installer source are provided under MIT. The original extracted code was generated for this project/user and reused with the user's explicit request. No third-party source library or executable was copied into the application.

Runtime references are Windows/.NET Framework assemblies: mscorlib, System, System.Core, System.Drawing, System.Windows.Forms, System.Web.Extensions, System.Xaml, WindowsBase, PresentationCore and PresentationFramework. They are **referenced from the installed Microsoft runtime, not redistributed** in these packages. Microsoft component licensing remains Microsoft's. The Windows Framework compiler and WPF runtime are build prerequisites.

Windows interface declarations and GUID constants express public API contracts. Vendor-specific Sonar metadata constants are documented as compatibility assumptions, not licensed vendor source. No Sonar/Discord binaries, presets, account files, logos or proprietary utilities are distributed.

The GitHub workflow references GitHub's maintained checkout/upload-artifact actions for CI only; those are not runtime dependencies. No package-manager downloads are needed to build the supplied script.

MIT permits free redistribution of EchoGuard source/binaries subject to its notice. It does not grant rights to Microsoft, Discord or SteelSeries trademarks or claim vendor endorsement. A maintainer should review this dependency list, license, signing and public-release checklist before publishing.


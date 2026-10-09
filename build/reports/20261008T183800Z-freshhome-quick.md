# Verify quick

UTC: 20261008T183800Z

```
a97dd02ff5bd8c6e708c42e548dbd9b8052ac532c683508d817d9e18eef6e205  verify/thresholds.toml
9393c5e58e9bfd77f7c035f8a8d056fc7822f6100720cc893467190dde41a8ca  verify/ref_coverage.toml
a97dd02ff5bd8c6e708c42e548dbd9b8052ac532c683508d817d9e18eef6e205  verify/thresholds.toml
```

## Result

Gates: G1 G2 G3 G10 G14
Failures: 0
Log: /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/build/reports/20261008T183800Z-quick.log

## Log

```

## G1

Welcome to .NET 10.0!
---------------------
SDK Version: 10.0.401

Telemetry
---------
The .NET tools collect usage data in order to help us improve your experience. It is collected by Microsoft and shared with the community. You can opt-out of telemetry by setting the DOTNET_CLI_TELEMETRY_OPTOUT environment variable to '1' or 'true' using your favorite shell.

Read more about .NET CLI Tools telemetry: https://aka.ms/dotnet-cli-telemetry

----------------
Installed an ASP.NET Core HTTPS development certificate.
To trust the certificate, run 'dotnet dev-certs https --trust'
Learn about HTTPS: https://aka.ms/dotnet-https

----------------
Write your first app: https://aka.ms/dotnet-hello-world
Find out what's new: https://aka.ms/dotnet-whats-new
Explore documentation: https://aka.ms/dotnet-docs
Report issues and find source on GitHub: https://github.com/dotnet/core
Use 'dotnet --help' to see available commands or visit: https://aka.ms/dotnet-cli
--------------------------------------------------------------------------------------
  Determining projects to restore...
  Restored /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim/Headless/SixActual.Headless.csproj (in 51 ms).
  Restored /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim/SixActual.Sim.csproj (in 44 ms).
  Restored /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/game/SixActual.csproj (in 129 ms).
  Restored /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/analyzers/SixActual.Analyzers.csproj (in 3.35 sec).
  Restored /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim.Tests/SixActual.Sim.Tests.csproj (in 3.35 sec).
  SixActual.Analyzers -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/analyzers/bin/Release/netstandard2.0/SixActual.Analyzers.dll
  SixActual.Sim -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim/bin/Release/net8.0/SixActual.Sim.dll
  SixActual.Headless -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim/Headless/bin/Release/net8.0/SixActual.Headless.dll
  SixActual.Sim.Tests -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim.Tests/bin/Release/net8.0/SixActual.Sim.Tests.dll
  SixActual -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/game/.godot/mono/temp/bin/Release/SixActual.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.02
G1: pass

## G2
  Determining projects to restore...
  Restored /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim/SixActual.Sim.csproj (in 38 ms).
  Restored /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/analyzers/SixActual.Analyzers.csproj (in 101 ms).
  1 of 3 projects are up-to-date for restore.
  SixActual.Analyzers -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/analyzers/bin/Release/netstandard2.0/SixActual.Analyzers.dll
  SixActual.Sim -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim/bin/Release/net8.0/SixActual.Sim.dll
  SixActual.Sim.Tests -> /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim.Tests/bin/Release/net8.0/SixActual.Sim.Tests.dll
Test run for /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/sim.Tests/bin/Release/net8.0/SixActual.Sim.Tests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.

Passed!  - Failed:     0, Passed:    54, Skipped:     0, Total:    54, Duration: 3 s - SixActual.Sim.Tests.dll (net8.0)

Attachments:
  /tmp/claude-1000/-home-baney75-Projects/44813eff-1f5e-59fd-af24-c4a0a3152f94/scratchpad/fresh/repo/build/coverage/04876af5-aa2a-4f3e-a3e0-5adea72e7243/coverage.cobertura.xml
sim line coverage 0.947 (921/973), minimum 0.800
G2: pass

## G3
seed=1 threads=1 tick=100 hash=4a0d9049be79b71e7c10f532000bf573bb8a89f0f62fa78fc992446e824685f7
seed=1 threads=1 tick=1000 hash=0621964f20963b204456328c54819d7eac6ef0dff330bd9b6816acc46294fb5d
seed=1 threads=1 tick=12000 hash=a98f6bb094915bb96b83b8068ab1abe7446bd1b0dfe9bbba78c12334888dfbcb
seed=1 threads=8 tick=100 hash=4a0d9049be79b71e7c10f532000bf573bb8a89f0f62fa78fc992446e824685f7
seed=1 threads=8 tick=1000 hash=0621964f20963b204456328c54819d7eac6ef0dff330bd9b6816acc46294fb5d
seed=1 threads=8 tick=12000 hash=a98f6bb094915bb96b83b8068ab1abe7446bd1b0dfe9bbba78c12334888dfbcb
seed=2 threads=1 tick=100 hash=35219bbd1d606285cc4104cf9d750cbdf4b618693a6bca68e356de9b81f4d98d
seed=2 threads=1 tick=1000 hash=220e2546145dd40c25c6d8ffaf27889715f06766d60ad8180eb7ae980f5d8cb7
seed=2 threads=1 tick=12000 hash=d537e595aa17c4b32a46a13de78e84b8158f3acf760aa5864ea400e2bea7702b
seed=2 threads=8 tick=100 hash=35219bbd1d606285cc4104cf9d750cbdf4b618693a6bca68e356de9b81f4d98d
seed=2 threads=8 tick=1000 hash=220e2546145dd40c25c6d8ffaf27889715f06766d60ad8180eb7ae980f5d8cb7
seed=2 threads=8 tick=12000 hash=d537e595aa17c4b32a46a13de78e84b8158f3acf760aa5864ea400e2bea7702b
seed=3 threads=1 tick=100 hash=45af395bee57ae5d997108d19b5076d42ec84ec46d8972d49594ff051de60be4
seed=3 threads=1 tick=1000 hash=4a3dcb4eaa90027722021581719032458d17bfcbdf5725d9dcf81acffe64cab3
seed=3 threads=1 tick=12000 hash=97ae7c0804a69ffc02ffc7a8f11157ab5724d55dac621788bcd610d22e1e8eeb
seed=3 threads=8 tick=100 hash=45af395bee57ae5d997108d19b5076d42ec84ec46d8972d49594ff051de60be4
seed=3 threads=8 tick=1000 hash=4a3dcb4eaa90027722021581719032458d17bfcbdf5725d9dcf81acffe64cab3
seed=3 threads=8 tick=12000 hash=97ae7c0804a69ffc02ffc7a8f11157ab5724d55dac621788bcd610d22e1e8eeb
replay=ok hash=af72df67afeda869a1b30c01d5f73a8e3a7f532afdcf4cc569bc1ab5ff348670
lockstep_clients=8 seed=4 tick=1000 hash=e3ada8c4d3866c468be5553bb3fe0429ffc2fa5fc4ae6889aeafb49530b465e0
selftest hash=842055ead8b768f51de8003687c2a2ef3a8516d9109df9962b8f714849323669
G3: pass

## G10
Installed 14 packages in 5ms
ok -- validation done
ok -- validation done
catalog ok: 3 eras, 0 catalog units, 103 roster units
requirements: 88 in brief, 88 tracked, 0 problems
registry current
G10: pass

## G14
G14: 1 registry items, 1 in production, 0 shippable files scanned
selftest good: ok (0 problems)
G14: 1 registry items, 1 in production, 0 shippable files scanned
selftest missing slot: ok (1 problems)
G14: 1 registry items, 1 in production, 0 shippable files scanned
selftest unsourced number: ok (1 problems)
G14: 1 registry items, 1 in production, 0 shippable files scanned
selftest unlicensed ref: ok (1 problems)
G14: 1 registry items, 1 in production, 1 shippable files scanned
selftest leaked file: ok (1 problems)
G14: 1 registry items, 1 in production, 1 shippable files scanned
selftest leaked path: ok (1 problems)
G14: 1 registry items, 1 in production, 0 shippable files scanned
selftest look-only outside refs_private: ok (1 problems)
G14: 103 registry items, 0 in production, 384 shippable files scanned
G14: pass
```

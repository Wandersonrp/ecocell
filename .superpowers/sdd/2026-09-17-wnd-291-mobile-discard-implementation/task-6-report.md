# Task 6 report

## Status

Task 6 review fixes applied to the pending-discard monitor and Android notification setup.

## Monitor-specific evidence

- Every refresh captures both the active-context generation and a monitor generation. Restart increments the monitor generation, cancels the previous loop, and invalidates its refresh sequence.
- The response path checks foreground/auth/context eligibility and all generations before entering a shared side-effect gate. `RestartAsync` uses the same gate, so notification display and baseline/count persistence cannot interleave with restart invalidation.
- Restart clears `PendingCount` and `IsLoading`, and emits `Changed` so the Home badge cannot retain the previous point's value; target clearing is limited to invalidating session or point transitions.
- Notification taps are accepted only while authenticated. Session generation changes only for an actual new sign-in or logout; token initialization leaves it unchanged, preserving a cold-start target through `InitializeAsync`. A transition to another collector point clears the target, while `TryTakeNotificationTarget` retains it for the `Routes` consumer until explicitly taken.
- Android now declares `android.permission.POST_NOTIFICATIONS`.
- Persistence remains per collector point and stores only the current discard ID set; notification text contains only an aggregate count and the point ID is carried as returning data.

## Tests

- `rtk dotnet build src/Ecocell.Mobile/Ecocell.Mobile.csproj -f net10.0-windows10.0.19041.0 --no-restore`: passed, 2 projects, 0 errors, 0 warnings.
- `rtk dotnet test Ecocell.slnx --no-restore`: final post-change run passed, 500 tests, 0 failures, 3 runner warnings.

## Technical smoke

Not executed: it requires a running local API, an active collector point, and interactive MAUI notification/lifecycle control.

## Concerns

- Monitor-specific automated tests are not present in the existing test projects; validation is compile plus the repository suite.
- The requested manual smoke scenarios remain to be exercised on a running MAUI client/API pair.

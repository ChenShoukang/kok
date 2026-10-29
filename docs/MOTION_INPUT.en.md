# Motion and input responsiveness

[中文](MOTION_INPUT.md) · [Agent interactions](AGENT_INTERACTIONS.en.md)

A damped, bounded spring progress gives entrances, menus, drawers and choice indicators consistent timing. Visual transitions are separate from input: page state commits synchronously, while button and menu hit rectangles stay in place. Icons, covers, indicators and noninteractive old-content snapshots may move. Native window commands are sent immediately without waiting for an exit animation.

Fixed stale page updates overwriting a newer Agent route, ordinary UI updates dismissing open selects, and popup hit rectangles moving during entry. Agent retains its open selects and composer. Shared menus only rebind in the same workspace/dialog with matching options and value. Favorite and more controls have 32 px targets. Decorative snapshots and notifications do not intercept clicks.

Interruptions cancel older motion and preserve the latest state. Existing move/resize settling remains in place; page hiding and reduced-motion preferences finish motion immediately. No Remotion, Motion or Radix runtime dependency was added.

References: [Remotion spring](https://www.remotion.dev/docs/spring), [Motion animate](https://motion.dev/docs/animate), and [Radix animation presence](https://www.radix-ui.com/primitives/docs/guides/animation). These inform timing and interaction boundaries, not a transplanted renderer or component implementation.

Validation uses generated files in silent isolated windows: rapid navigation, clicks during motion, retained menus, repeated opening/closing, settings switches, reduced motion and real native move/resize gestures. No real AI service is called. Local reports are kept under `test-output`.

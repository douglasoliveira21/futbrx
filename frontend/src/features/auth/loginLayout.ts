// Reference composition of the mockup (1648 x 928); every element of the desktop layout is positioned in these pixels and the
// whole stage is scaled to the viewport.
export const STAGE = { width: 1648, height: 928 };
export const stageScale = (width: number, height: number) => Math.min(width / STAGE.width, height / STAGE.height);
// Below this width the composition would be unreadable, so the layout becomes a normal column.
export const COMPACT_WIDTH = 900;

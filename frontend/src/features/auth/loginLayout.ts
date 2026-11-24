// Reference composition of the mockup; every element of the desktop layout is positioned in these pixels and the
// whole stage is scaled to the viewport.
// The mockup is 1648 x 928; the stage is 52 px taller so the competitions block has room below the login panel.
export const STAGE = { width: 1648, height: 980 };
// The composition fills the window at 100%, which felt too big on real screens: it is drawn a little smaller, centred.
export const CONTENT_SCALE = 0.85;
export const stageScale = (width: number, height: number) => Math.min(width / STAGE.width, height / STAGE.height) * CONTENT_SCALE;
// Below this width the composition would be unreadable, so the layout becomes a normal column.
export const COMPACT_WIDTH = 900;

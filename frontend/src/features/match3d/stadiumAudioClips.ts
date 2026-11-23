// Offsets in the decoded recordings, in seconds (not match-clock seconds).
// Goal.wav contains pre-goal ambience until the roar at 5.65 s.
// Keep the original CC0 file/hash intact and edit its playback window.
export function stadiumAudioClip(
  name: string,
  bufferDuration: number,
  pitch: number,
  maxDuration: number,
) {
  const offset = name === "goal" ? Math.min(5.65, bufferDuration) : 0;
  const end = name === "goal" ? Math.min(12, bufferDuration) : bufferDuration;
  return {
    offset,
    duration: Math.max(0, Math.min(maxDuration, (end - offset) / pitch)),
  };
}

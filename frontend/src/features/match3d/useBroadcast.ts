import { useEffect, useRef, useState } from "react";
import {
  api,
  type BroadcastPacket,
  type BroadcastSession,
} from "../../services/api";

export function useBroadcast(
  session: BroadcastSession | undefined,
  career: string,
  playing: boolean,
  visible: boolean,
) {
  const [packet, setPacket] = useState<BroadcastPacket>();
  const [error, setError] = useState("");
  const clientId = useRef(crypto.randomUUID());
  const latest = useRef<BroadcastPacket | undefined>(undefined);
  const [controlBusy, setControlBusy] = useState(false),
    [controlError, setControlError] = useState("");
  const controlling = useRef(false),
    inFlight = useRef<Promise<BroadcastPacket> | undefined>(undefined);
  const active = useRef({ playing, visible });
  active.current = { playing, visible };
  useEffect(() => {
    if (!session) return;
    let disposed = false,
      timer: ReturnType<typeof setTimeout>;
    latest.current = undefined;
    async function poll() {
      if (controlling.current) {
        timer = setTimeout(poll, 100);
        return;
      }
      try {
        const previous = latest.current;
        const request = api<BroadcastPacket>(`/careers/${career}/live/tick`, {
          id: session!.id,
          clientId: clientId.current,
          playing:
            active.current.playing &&
            active.current.visible &&
            !document.hidden,
          after: previous?.replay.frames.at(-1)?.second ?? -1,
        });
        inFlight.current = request;
        const result = await request;
        if (disposed) return;
        result.replay.frames = [
          ...(previous?.replay.frames ?? []),
          ...result.replay.frames,
        ];
        latest.current = result;
        setPacket(result);
        setError("");
      } catch (e) {
        if (!disposed)
          setError(
            e instanceof Error
              ? e.message
              : "Conexão interrompida. Retomando transmissão…",
          );
      } finally {
        if (!disposed && !latest.current?.complete)
          timer = setTimeout(poll, 1000);
      }
    }
    void poll();
    return () => {
      disposed = true;
      clearTimeout(timer);
    };
  }, [session?.id, career]);
  async function control(body: object) {
    if (!session || controlling.current) return false;
    controlling.current = true;
    setControlBusy(true);
    setControlError("");
    try {
      await inFlight.current?.catch(() => undefined);
      const updated = await api<BroadcastSession>(
        `/careers/${career}/live/control`,
        { id: session.id, ...body },
      );
      if (latest.current) {
        latest.current = { ...latest.current, session: updated };
        setPacket(latest.current);
      }
      return true;
    } catch (e) {
      setControlError(
        e instanceof Error ? e.message : "Não foi possível aplicar o comando.",
      );
      return false;
    } finally {
      controlling.current = false;
      setControlBusy(false);
    }
  }
  return { packet, error, control, controlBusy, controlError };
}

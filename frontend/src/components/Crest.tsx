import React, { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Shield } from "lucide-react";
import type { Club } from "../services/api";

// Club crest: the licensed image when one is included, otherwise a shield in the club colour.
export function Crest({ club, size = "" }: { club?: Club; size?: string }) {
  const credits = useQuery<{
    clubs: { clubId: string; status: string; path: string | null }[];
  }>({
    queryKey: ["crest-credits"],
    queryFn: async () => {
      const r = await fetch("/data/crest-credits.json");
      if (!r.ok) throw new Error("Créditos indisponíveis");
      return r.json();
    },
    staleTime: Infinity,
  });
  // Escudo enviado pelo administrador tem prioridade sobre o escudo incluído no projeto.
  const logos = useQuery<{ clubId: string; v: string }[]>({
    queryKey: ["logos"],
    queryFn: async () => {
      const r = await fetch("/api/logos");
      if (!r.ok) throw new Error("Escudos indisponíveis");
      return r.json();
    },
    staleTime: 60_000,
  });
  const uploaded = logos.data?.find((l) => l.clubId === club?.id);
  const [failed, setFailed] = useState(false);
  const crest = uploaded
    ? { path: `/api/logos/${uploaded.clubId}?v=${uploaded.v}`, status: "included", clubId: uploaded.clubId }
    : undefined;
  const bundled = credits.data?.clubs.find(
    (c) => c.clubId === club?.id && c.status === "included",
  );
  const shown = crest ?? bundled;
  if (shown?.path && !failed)
    return (
      <span className={"crest " + size}>
        <img
          src={shown.path}
          alt={"Escudo de " + club?.name}
          title="Créditos e licença em Dados e licenças"
          style={{
            width: size === "xl" ? 96 : size === "large" ? 54 : 30,
            height: size === "xl" ? 104 : size === "large" ? 58 : 34,
            objectFit: "contain",
          }}
          onError={() => setFailed(true)}
        />
      </span>
    );

  return (
    <span
      className={"crest " + size}
      style={{ "--club": club?.color ?? "#17d994" } as React.CSSProperties}
    >
      <Shield
        size={size === "xl" ? 92 : size === "large" ? 51 : 27}
        fill="currentColor"
        strokeWidth={1}
      />
      <b>{club?.short ?? "BM"}</b>
    </span>
  );
}

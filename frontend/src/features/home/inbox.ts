import type { Career, Club, Fixture, Player } from "../../services/api";

// The coach's inbox: what changed and what needs a decision, written as messages from
// the people around the club. Everything is derived from the saved career state.
export type InboxSender =
  | "president"
  | "director"
  | "assistant"
  | "medical"
  | "fitness"
  | "legal"
  | "press";
export type InboxTone = "urgent" | "normal" | "good";
export type InboxMessage = {
  id: string;
  sender: InboxSender;
  from: string;
  role: string;
  tone: InboxTone;
  title: string;
  body: string;
  action?: { label: string; path: string };
};

const toneOrder: Record<InboxTone, number> = { urgent: 0, normal: 1, good: 2 };
// Internal maintenance notes are kept in the club news, but they are not stories for the coach.
const technicalNews = /migra|snapshot|vers[aã]o do (motor|save)|revis[aã]o/i;

export function nextFixture(c: Career): Fixture | undefined {
  const g = c.state;
  return (
    g.nextCup ??
    g.fixtures.find(
      (f) =>
        f.round === g.round && (f.home === g.clubId || f.away === g.clubId),
    )
  );
}

export function clubOf(c: Career, id: string): Club | undefined {
  const g = c.state;
  return (
    g.clubs.find((x) => x.id === id) ??
    g.competitionClubs?.find((x) => x.id === id) ??
    g.otherClubs?.find((x) => x.id === id)
  );
}

const names = (players: Player[]) => {
  const list = players.map((p) => p.name);
  return list.length <= 3
    ? list.join(", ")
    : `${list.slice(0, 3).join(", ")} e mais ${list.length - 3}`;
};

export function buildInbox(
  c: Career,
  money: (n: number) => string,
): InboxMessage[] {
  const g = c.state;
  const club = clubOf(c, g.clubId);
  const next = nextFixture(c);
  const opponent = next
    ? clubOf(c, next.home === g.clubId ? next.away : next.home)
    : undefined;
  const played = g.fixtures.some(
    (f) => f.homeGoals !== null && (f.home === g.clubId || f.away === g.clubId),
  );
  const messages: InboxMessage[] = [];

  if (g.coach?.unreadDismissal)
    messages.push({
      id: "dismissal",
      sender: "president",
      from: "Presidente",
      role: "Diretoria",
      tone: "urgent",
      title: "Seu ciclo no clube terminou",
      body: "A diretoria decidiu encerrar seu contrato. Procure um novo desafio no mercado de técnicos.",
      action: { label: "Ver minha carreira", path: "/minha-carreira" },
    });

  if (!played && g.season === 2026 && g.round === 1)
    messages.push({
      id: "welcome",
      sender: "president",
      from: "Presidente",
      role: "Diretoria",
      tone: "normal",
      title: "Bem-vindo, treinador",
      body: `O elenco do ${club?.name ?? "clube"} está à sua disposição. Conheça os jogadores e defina a escalação antes da estreia.`,
      action: { label: "Conhecer o elenco", path: "/elenco" },
    });

  for (const o of g.offers.filter((o) =>
    ["agreed", "countered"].includes(o.status),
  ).slice(0, 3)) {
    const buyer = clubOf(c, o.toClub)?.name ?? "Um clube";
    const seller = clubOf(c, o.fromClub)?.name ?? "O clube vendedor";
    const countered = o.status === "countered";
    messages.push({
      id: "offer-" + o.id,
      sender: "director",
      from: "Diretor de futebol",
      role: "Mercado",
      tone: "urgent",
      title:
        o.kind === "outbound"
          ? `Proposta por ${o.playerName}`
          : o.kind === "renewal"
            ? `Renovação de ${o.playerName}`
            : countered
              ? `Contraproposta por ${o.playerName}`
              : `${o.playerName} aceitou os termos`,
      body:
        o.kind === "outbound"
          ? `${buyer} oferece ${money(o.fee)} e aguarda sua decisão.`
          : o.kind === "renewal"
            ? `O jogador ${countered ? "fez uma contraproposta" : "aceitou a renovação"}. Falta sua confirmação.`
            : countered
              ? `${seller} pede ${money(o.counterFee ?? o.fee)}. Responda antes que a proposta expire.`
              : `Acordo com ${seller} por ${money(o.fee)}. Falta sua confirmação.`,
      action: { label: "Responder", path: "/transferencias" },
    });
  }

  const unavailable = g.squad.filter(
    (p) => p.starter && (p.injuryDays > 0 || p.suspensionMatches > 0),
  );
  if (next && unavailable.length)
    messages.push({
      id: "unavailable-starters",
      sender: "assistant",
      from: "Auxiliar técnico",
      role: "Comissão técnica",
      tone: g.autoReplaceUnavailable ? "normal" : "urgent",
      title:
        unavailable.length === 1
          ? "Um titular está fora do próximo jogo"
          : `${unavailable.length} titulares estão fora do próximo jogo`,
      body: `${names(unavailable)}. ${g.autoReplaceUnavailable ? "A substituição automática vai cobrir a ausência, mas vale revisar." : "Ajuste a escalação antes de entrar em campo."}`,
      action: { label: "Ajustar escalação", path: "/elenco" },
    });

  const tired = g.squad.filter(
    (p) =>
      p.starter && p.injuryDays === 0 && p.suspensionMatches === 0 && p.fitness < 75,
  );
  if (next && tired.length)
    messages.push({
      id: "tired-starters",
      sender: "fitness",
      from: "Preparador físico",
      role: "Comissão técnica",
      tone: "normal",
      title: "Titulares com desgaste físico",
      body: `${names(tired)} ${tired.length === 1 ? "está" : "estão"} abaixo de 75% de condição.`,
      action: { label: "Ver treinamentos", path: "/treinamentos" },
    });

  const injured = g.squad.filter((p) => !p.starter && p.injuryDays > 0);
  if (injured.length)
    messages.push({
      id: "injuries",
      sender: "medical",
      from: "Departamento médico",
      role: "Saúde",
      tone: "normal",
      title:
        injured.length === 1
          ? `${injured[0].name} segue em tratamento`
          : `${injured.length} jogadores em tratamento`,
      body:
        injured.length === 1
          ? `${injured[0].injuryName || "Lesão"} · previsão de ${injured[0].injuryDays} dias.`
          : names(injured) + ".",
      action: { label: "Departamento médico", path: "/medico" },
    });

  const transferWindow = c.management.window;
  if (transferWindow.isOpen && transferWindow.closingRound !== null) {
    const closing = transferWindow.closingRound - g.round;
    messages.push({
      id: "transfer-window",
      sender: "director",
      from: "Diretor de futebol",
      role: "Mercado",
      tone: closing <= 1 ? "urgent" : "normal",
      title:
        closing <= 0
          ? "A janela de transferências fecha nesta rodada"
          : `Janela aberta até a rodada ${transferWindow.closingRound}`,
      body: `Caixa disponível de ${money(g.balance)} para reforçar o elenco.`,
      action: { label: "Abrir mercado", path: "/transferencias" },
    });
  }

  if (c.management.expiringContracts > 0)
    messages.push({
      id: "expiring-contracts",
      sender: "legal",
      from: "Departamento jurídico",
      role: "Contratos",
      tone: g.round >= 30 ? "urgent" : "normal",
      title:
        c.management.expiringContracts === 1
          ? "Um contrato termina nesta temporada"
          : `${c.management.expiringContracts} contratos terminam nesta temporada`,
      body: "Renove quem você quer manter. Sem renovação, o jogador sai livre na virada do ano.",
      action: { label: "Ver contratos", path: "/contratos" },
    });

  const confidence = g.board?.confidence;
  if (confidence !== undefined && !g.coach?.unreadDismissal) {
    if (confidence < 45)
      messages.push({
        id: "board-pressure",
        sender: "president",
        from: "Presidente",
        role: "Diretoria",
        tone: "urgent",
        title: "A diretoria cobra resultados",
        body: `A confiança no seu trabalho está em ${confidence}%. Os próximos jogos serão decisivos.`,
        action: { label: "Falar com a diretoria", path: "/diretoria" },
      });
    else if (confidence >= 80 && played)
      messages.push({
        id: "board-praise",
        sender: "president",
        from: "Presidente",
        role: "Diretoria",
        tone: "good",
        title: "A diretoria está satisfeita",
        body: `A confiança no seu trabalho chegou a ${confidence}%.`,
      });
  }

  if (next && opponent && !messages.some((m) => m.sender === "assistant"))
    messages.push({
      id: "match-ready",
      sender: "assistant",
      from: "Auxiliar técnico",
      role: "Comissão técnica",
      tone: "good",
      title: `Time pronto para encarar ${opponent.name}`,
      body: `${g.squad.filter((p) => p.starter).length} titulares no ${g.formation}, mentalidade ${g.mentality.toLowerCase()}.`,
      action: { label: "Revisar táticas", path: "/taticas" },
    });

  g.news
    .filter((n) => n.trim() && !technicalNews.test(n))
    .slice(0, 3)
    .forEach((n, i) =>
      messages.push({
        id: "news-" + i,
        sender: "press",
        from: "Imprensa",
        role: "Notícias",
        tone: "normal",
        title: n.length > 90 ? n.slice(0, 87).trimEnd() + "…" : n,
        body: "",
        action: i === 0 ? { label: "Ver imprensa", path: "/imprensa" } : undefined,
      }),
    );

  return messages
    .map((m, i) => ({ m, i }))
    .sort((a, b) => toneOrder[a.m.tone] - toneOrder[b.m.tone] || a.i - b.i)
    .map(({ m }) => m);
}

export type FormResult = "V" | "E" | "D";
export function recentForm(c: Career, clubId: string, count = 5) {
  return c.state.fixtures
    .filter(
      (f) =>
        f.homeGoals !== null &&
        f.awayGoals !== null &&
        (f.home === clubId || f.away === clubId),
    )
    .slice(-count)
    .map((f) => {
      const own = f.home === clubId ? f.homeGoals! : f.awayGoals!;
      const other = f.home === clubId ? f.awayGoals! : f.homeGoals!;
      return {
        fixture: f,
        result: (own > other ? "V" : own === other ? "E" : "D") as FormResult,
      };
    });
}

// The leader plus the clubs around the coach's own position, so the table fits a side card.
export function tableAround(c: Career, span = 2) {
  const index = c.table.findIndex((r) => r.club.id === c.state.clubId);
  if (index < 0) return c.table.slice(0, 5).map((row, i) => ({ row, position: i + 1 }));
  const start = Math.max(0, Math.min(index - span, c.table.length - (span * 2 + 1)));
  const rows = c.table
    .slice(start, start + span * 2 + 1)
    .map((row, i) => ({ row, position: start + i + 1 }));
  if (start > 0) rows.unshift({ row: c.table[0], position: 1 });
  return rows;
}

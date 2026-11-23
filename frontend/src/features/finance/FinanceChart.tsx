import type { Career } from "../../services/api";
import {
  AreaChart,
  Area,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
} from "recharts";
export default function FinanceChart({ c }: { c: Career }) {
  const data = Array.from(
    { length: Math.max(1, Math.min(c.state.round - 1, 8)) },
    (_, i) => {
      const round = Math.max(1, c.state.round - 8) + i;
      return {
        name: "R" + round,
        receita:
          c.state.ledger
            .filter(
              (l) =>
                l.season === c.state.season &&
                l.round === round &&
                l.amount > 0,
            )
            .reduce((a, l) => a + l.amount, 0) / 1000000,
        despesa:
          -c.state.ledger
            .filter(
              (l) =>
                l.season === c.state.season &&
                l.round === round &&
                l.amount < 0,
            )
            .reduce((a, l) => a + l.amount, 0) / 1000000,
      };
    },
  );
  return (
    <div className="chart-area">
      <ResponsiveContainer width="100%" height={180}>
        <AreaChart data={data}>
          <defs>
            <linearGradient id="fill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#17d994" stopOpacity={0.3} />
              <stop offset="100%" stopColor="#17d994" stopOpacity={0} />
            </linearGradient>
          </defs>
          <XAxis
            dataKey="name"
            stroke="#80929e"
            tickLine={false}
            axisLine={false}
          />
          <YAxis
            stroke="#80929e"
            tickLine={false}
            axisLine={false}
            unit=" M"
            width={50}
          />
          <Tooltip
            contentStyle={{
              background: "#1c2d38",
              borderColor: "#344955",
              borderRadius: 8,
            }}
          />
          <Area
            type="monotone"
            dataKey="receita"
            name="Receitas (R$ M)"
            stroke="#17d994"
            fill="url(#fill)"
          />
          <Area
            type="monotone"
            dataKey="despesa"
            name="Despesas (R$ M)"
            stroke="#e8b75b"
            fill="transparent"
          />
        </AreaChart>
      </ResponsiveContainer>
    </div>
  );
}

# Entrega 0.12 — futebol ao vivo e ambiente de estádio

## Funcionamento

O positional-v5 avança em checkpoints de dois segundos enquanto a transmissão é acompanhada. O snapshot inicial não contém o resultado. Ao reiniciar, seed e preparação imutável reconstroem somente o passado assistido. Replays v1 a v4 mantêm seus motores. Pausa, tempo offline e múltiplas abas continuam sob relógio do servidor. O acompanhamento encerra suas requisições ao concluir, evitando disputar a gravação do resultado.

Há aceleração, fadiga, marcação com distribuição de adversários, seleção de passes por espaço e trajetórias aéreas. Impedimento considera partes válidas do corpo e participação do receptor. Cartões usam contexto de imprudência, temeridade, força excessiva e oportunidade clara de gol. Ainda faltam vantagem completa, mãos, VAR, interferência sem toque e outras situações arbitrais.

## Estádio e áudio

Arquibancadas em anel com degraus, cadeiras, corredores, cobertura, estruturas e refletores; público instanciado com corpos, braços e animações de vibração. Quantidade varia conforme a qualidade. Bandeiras de tecido ondulam com o vento. A bola tem diâmetro visual de 22 cm e 32 painéis. Traves cilíndricas respeitam abertura de 7,32 × 2,44 m; redes têm teto, laterais e fundo, com reação ao gol.

A energia da torcida aumenta conforme a bola chega à área. Gols, defesas e finalizações próximas recebem reações distintas, sem usar eventos futuros. Passe/chute usam o instante de contato informado pelo servidor. Apitos acompanham início, faltas, impedimento, intervalo e final. Há volume, ativação, medidor de saída e botões para testar cada efeito em Áudio do estádio.

Cinco gravações CC0 1.0 foram incorporadas: ambiente/Goal/Oehh por Sandermotions, Soccer Ball Kick por bittermelonheart e Referee whistle por Rosa-Orenes256. Fontes, licença e SHA-256 estão em `frontend/public/assets/audio/credits.json`; atribuição também aparece no jogo. São sons genéricos de estádio, sem atribuição a uma torcida brasileira. WebAudio adiciona posicionamento, reverberação e camadas de apoio; exige interação para ativação conforme o navegador.

## Gestão e persistência

Elenco em cartões, perfil individual, prancheta com troca de titular/reserva e mercado em cartões de observação. Trocas atômicas validam disponibilidade e goleiro no servidor.

`scripts/start-local.ps1` inicia API e interface locais e verifica prontidão. SQLite em `.runtime/local-game.db` e chave local de autenticação persistem entre reinícios. O modo InMemory continua opcional e descartável. Não recupera dados de processos em memória encerrados anteriormente.

## Validação

Build .NET sem avisos/erros; suíte de simulação com 281 verificações, API com 103, testes adicionais de retomada/SQLite e de limite exato do relógio aprovados. Smoke HTTP de autenticação, isolamento e bloqueio de saltos aprovado. Testes JavaScript conferem tamanho/painéis da bola, distribuição da arquibancada, energia sem antecipação, cues sem repetição e hashes/licenças dos cinco sons. Build TypeScript/Vite aprovado, com aviso de bundles grandes.

No navegador: partida completa Fluminense 3–0 Fortaleza, retorno à rodada 2, retomada, cartões/prancheta em desktop e celular, estádio e carregamento dos cinco sons, medidor com sinal de áudio. A conferência de saída não substitui avaliação auditiva do usuário nem medições em vários dispositivos.

## Limites

Visual estilizado, sem qualidade fotorealista ou animações profissionais por captura. Arbitragem ainda parcial, sem física articulada completa. Seis escudos incorporados; catorze pendentes, dos quais dois sem aprovação de origem/licença e doze com download limitado pelo provedor. A base de atletas reais de 2026 continua em coleta/revisão, sem publicação de elenco incompleto. PostgreSQL e migrações de produção não foram executados neste ambiente. Calendários próprios por temporada atendem ao escopo, sem necessidade de reproduzir a tabela oficial.

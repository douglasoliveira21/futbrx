# 0.14 — Comando à beira do campo

Novas partidas usam `positional-v7`. Replays e partidas já iniciadas em V1–V6 continuam nos respectivos motores.

## Durante a transmissão

- Multiplicadores 1×, 2×, 4×, 6×, 8×, 12×, 24×, 48× e 96×, ajustáveis durante o jogo. A entrada continua com 12 segundos reais; não há botão para pular até o placar.
- **Banco e táticas** pausa a transmissão e permite mudar formação, mentalidade e registrar substituições. São cinco trocas e três paradas; trocas do intervalo não consomem parada. Planos e trocas automáticas existentes também contam no limite.
- O motor para no intervalo antes de calcular o segundo tempo. O vestiário abre automaticamente. **Ir para o segundo tempo** é obrigatório mesmo sem alterações.
- Abas pausadas não congelam outra aba ativa da mesma carreira. Batimentos compartilham um único relógio e não multiplicam a velocidade.
- Os comandos ficam no snapshot da partida e são reproduzidos de forma determinística. A estratégia escolhida vale para esta partida.

## Campo e apresentação

- Saídas pela lateral ultrapassam a linha antes da reposição; jogador cobra com as mãos e o lançamento não gera impedimento direto.
- Impedimentos consideram participação do receptor. Faltas na área geram pênalti, com tempo para cobrança antes do encerramento; o cobrador fica parado na marca e protegido de desarme durante a preparação.
- Cartões usam velocidade relativa, contato por trás, excesso de força e situação do ataque. Árbitro apresenta amarelo, vermelho e segundo amarelo; assistentes acompanham a linha defensiva e sinalizam impedimento.
- Relógio negativo da entrada foi corrigido. Fogos e fumaça usam a cor do mandante. Arquibancada sempre ocupada com predominância da torcida da casa, em todas as qualidades.
- Modelo FBX fornecido preservado. Banco de 29 clipes: laterais, passe, chute forte, pênalti, três comemorações, defesa rasteira/lateral, marcação e cartões. Defesas laterais espelhadas.
- `15_Goalkeeper_Save_01` corresponde à defesa lateral; a rasteira foi obtida de `17_Goalkeeper_Save_03`. Foram removidos os longos trechos de espera. O estúdio permite comparar a captura completa e o recorte com controle de quadros.

## Validação e limites

Testes de comandos verificam velocidade, passado imutável, substituição pendente duplicada, intervalo, reinício do motor, troca no intervalo e igualdade entre transmissão e replay. Testes de assets verificam 29 clipes, hashes, skinning finito, chão, espelhamento e tempos de contato. A bateria geral passou com 280 verificações, a API com 107 e os comandos com 23. Os seis arquivos de teste do frontend passaram. A interface foi testada localmente, incluindo troca em andamento, intervalo, segundo tempo, apito final e entrada com efeitos.

É uma simulação estilizada, com decisões probabilísticas e arbitragem simplificada em situações excepcionais. Animações foram adaptadas entre esqueletos diferentes; não equivalem a uma produção final de captura e ajuste manual de cada interação. Não houve teste em uma variedade de aparelhos físicos.

## Licenças pendentes

Seis escudos estão incorporados com créditos. Doze têm revisão registrada, mas o download retornou HTTP 429. Corinthians e Bragantino continuam sem uma concessão verificável adotada para seus escudos atuais. O manifesto `/data/crest-credits.json` identifica cada caso. Não foi declarada licença em nome de clubes ou autores.

O modelo e o pacote de mocap foram fornecidos pelo usuário para uso local. Seus direitos originais são preservados. A licença de redistribuição do pacote deve ser comprovada antes de disponibilizar esses assets publicamente. Fontes, recortes e hashes estão em `/assets/players/mocap-manifest.json`.

Teste em http://127.0.0.1:5173/ . Saves locais continuam em `.runtime/local-game.db`.

## Correção das luvas do goleiro

As peças ovais foram substituídas no renderer por luvas com a topologia das mãos do modelo fornecido: dedos e polegar completos, casca com espessura entre 3,5 e 5 mm, palma clara, dorso escuro e faixas no punho. A malha preserva os pesos do esqueleto, incluindo a torção do antebraço. Vértices duplicados nas emendas recebem o mesmo deslocamento para evitar frestas. Uma inspeção local em `/glove-inspection.html` permite conferir palma, dorso, cada mão e comparar a peça anterior. Teste específico verifica fixação e pesos em cinco animações.

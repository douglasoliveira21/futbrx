# Dia de jogo — versão 0.12

Atualização: [entrega 0.12](RELEASE-0.12.md).

## Fluxo

Jogar partida abre uma sessão única para a rodada. O servidor prepara um snapshot determinístico e o mantém privado. A carreira visível não recebe placares, estatísticas ou efeitos financeiros antecipados. A transmissão apresenta doze segundos de entrada dos times, seguidos de todos os lances a 12×. Não há barra de avanço, alteração de velocidade, botão de pular ou fechamento antes do fim. Há pausa, câmeras, qualidade e áudio.

O endpoint de acompanhamento usa o tempo do servidor e heartbeats. Não concede tempo por pausas ou intervalos superiores a três segundos sem comunicação, nem aceita tempo enviado pelo cliente. Duas abas compartilham o mesmo relógio. O cliente recebe somente frames e eventos até o instante autorizado, interpolando posições sem ultrapassar esse limite. O replay completo permanece indisponível até a conclusão. No apito final, Voltar ao vestiário aplica o resultado calculado e conclui os demais jogos da rodada; repetir a finalização não duplica a rodada.

Recarregar retoma a sessão e a configuração original. A persistência acompanha o banco configurado: o modo local padrão usa SQLite em arquivo e preserva contas e carreiras; somente o modo explicitamente InMemory perde os dados ao reiniciar. Não há garantia de impedir adulteração do software ou acesso direto de um administrador ao banco; o fluxo normal e as rotas públicas bloqueiam saltos.

## Apresentação

A arte de estádio em SVG é original do projeto. O tema de estádio noturno, tipografia esportiva e verde-lima se aplica à entrada, seleção de clube, vestiário e módulos de gestão. Câmera de TV, placar, relógio de tempos/acréscimos e aviso de gol apresentam a partida. O campo ganhou textura procedural, redes, pequena área, marcas de pênalti, bandeiras e túnel. Os perfis de qualidade continuam disponíveis. O modo tático mantém jogadores, bola e placar quando o 3D falha.

O motor incremental positional-v5 calcula decisões apenas até o relógio assistido; modelos e animações são estilizados. Não se trata de uma produção fotorealista, novo motor de física ou tomada de decisões esportivas interativa durante a transmissão. Partidas históricas já concluídas podem ser reproduzidas livremente.

## Verificação

`SimulationTests -- --broadcast` verifica ausência de efeitos antecipados, idempotência do início, bloqueio de encerramento, projeção sem resultado futuro, relógio de várias abas, offline/pausa, serialização/retomada, finalização e consistência com replay. A suíte API testa propriedade, bloqueios e entrega parcial. Os demais testes de gestão usam fixtures de banco com acompanhamento concluído, sem atalho exposto em produção. O smoke HTTP verifica o início real e as recusas. Testes JavaScript verificam interpolação limitada, pausa, suspensão e relógio com acréscimos.

A conferência no navegador cobriu larguras de celular e computador, pausa e retomada sem avanço do relógio, partida completa até 90+03:00 e retorno ao vestiário. O empate de teste foi registrado como 0–0, com a carreira na rodada 2 e um ponto na classificação. Build TypeScript/Vite, 261 verificações da simulação, 97 da API e smoke HTTP passaram.

## Lesões em jogo (motor positional-v15)

O motor v15 substitui o sorteio simples de lesão (as partidas antigas continuam como eram, com a versão do motor gravada):

- **Origem**: lesões por contato (após faltas, com mais chance quando há cartão, e após carrinhos; nunca em pênalti) e por desgaste (a
  chance cresce com o cansaço, e o atleta mais cansado é o mais provável). Máximo de uma lesão por equipe em cada partida; em média cerca
  de 0,6 lesão por jogo.
- **Tipo e gravidade**: por contato, contusão (3 a 7 dias), entorse de tornozelo (10 a 24), lesão no joelho (45 a 120) e fratura (90 a 180);
  por desgaste, lesão muscular (10 a 28), distensão (7 a 14) e tendinite (5 a 12). O nome e os dias vão no lance (`injury`, com
  `injuryName` e `injuryDays`) e para a ficha do atleta e a notícia do departamento médico.
- **No campo**: o atleta lesionado fica caído (ação `injured`; no 3D o corpo tomba e fica no chão), sai da disputa de bola e, se segue
  em campo, joga machucado (nota e condição reduzidas).
- **Troca automática ligada**: depois do atendimento, o auxiliar substitui o lesionado.
- **Troca automática desligada**: a transmissão **para** (`injuryAwaiting` na sessão) até o treinador decidir, numa janela que lista os
  reservas da mesma posição (nota e condição) e oferece "deixar o auxiliar escolher". A troca por lesão não usa uma das três paradas de
  substituição, mas conta como uma das cinco trocas. Sem trocas restantes (ou sem reserva da posição), só resta "continuar com dez"
  (`injury_retire`). O servidor recusa retirar quem não está aguardando decisão.
- **Replay**: a decisão é gravada como instrução da partida, então o replay refeito pela semente repete a mesma troca.

Limites: o jogo não para para o atendimento (a bola segue rolando enquanto o atleta está no chão) e não há maca nem fisioterapeuta
animados.

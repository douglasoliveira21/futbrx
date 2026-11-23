# Calendário das temporadas — 0.10

Cada carreira gera a própria tabela por ano e divisão. Não é necessário reproduzir partidas ou remarcações do Brasileirão real, nem aguardar uma data do mundo real para jogar.

O gerador usa vinte clubes, turno e returno, 38 rodadas e 380 jogos. Cada clube tem dezenove mandos. A tabela muda ao iniciar outro ano e é reproduzível a partir da mesma carreira, ano e divisão. A ordem de entrada dos clubes não altera o resultado.

As rodadas são semanais a partir do primeiro sábado de fevereiro. Jogos se distribuem entre sábado e domingo, com pelo menos cinco dias entre rodadas; a rodada 38 ocorre simultaneamente no domingo às 16h. Horários seguem UTC−03:00. As divisões usam tabelas próprias e os playoffs seguem as rodadas 39 e 40. É uma escolha de calendário do jogo, sem pretensão de reproduzir o regulamento de datas da CBF.

Saves antigos mantêm confrontos, sementes, resultados e replays. A migração só preenche datas ausentes em partidas futuras ainda sem configuração congelada. Calendários anteriores já datados permanecem preservados.

A suíte `--calendar` verifica 2026, 2027, 2028, 2035, 2050 e 2100, além de determinismo, mandos, descanso, ausência de duplicatas, migração idempotente e virada de temporada nas duas divisões.

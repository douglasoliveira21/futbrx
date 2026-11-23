# Regras, gestão e fontes de 2026 — versão 0.9

A versão 0.9 acrescenta regras ao motor, negociação financeira e duas divisões demonstrativas. A base integral de atletas reais de 2026 permanece em revisão e não foi publicada. Esta distinção também aparece no jogo.

## Gestão avançada

Em Transferências, a proposta de compra aceita 1–24 parcelas por rodada. A confirmação debita a entrada e as luvas; as obrigações restantes conservam o valor total, inclusive quando atravessam o ano. Cada parcela é liquidada uma vez no vencimento e aparece em Gestão avançada. O caixa pode ficar negativo: parcelar não elimina a dívida. Salários continuam por rodada, como no restante do manager.

Contratações e renovações podem definir uma multa rescisória entre zero e R$ 5 bilhões. Zero desativa a cláusula. Novos contratos demonstrativos iniciais usam duas vezes o valor de jogo. Contratos antigos preservam seus termos. Acionar a multa paga o valor contratual e negocia salário/luvas calculados pelo servidor; janela, revisão concorrente e limites de elenco continuam obrigatórios.

Gestão avançada permite receber ou ceder empréstimos até o fim da temporada. Taxa fixa demonstrativa de 5% do valor de jogo, mínimo R$ 50 mil, paga à vista; destino assume salário integral. O contrato de propriedade permanece na origem. Jogador emprestado não pode ser vendido, renovado como propriedade do destino nem emprestado novamente. A origem reserva sua vaga de retorno. Na virada, o empréstimo termina antes da análise de vencimentos dos contratos. Consultas e confirmações repetidas não duplicam atleta ou pagamento.

Não foram acrescentados empresários, percentuais de salário negociáveis, opções/obrigações de compra, bônus condicionais, antecipação de recebíveis ou regras contábeis reais. Taxas, valores e decisões dos clubes são parâmetros do jogo.

## Motor positional-v4

Os motores v1, v2 e v3 permanecem congelados. Só novas partidas usam v4.

- Impedimento no toque do passe para o destinatário que participa da jogada: campo adversário, além da bola e do penúltimo oponente. Em linha não é impedimento. Laterais, escanteios e tiros de meta têm a exceção da recepção direta.
- Separação cinemática dos centros dos atletas, com raio de 0,52 m, no servidor. É uma aproximação de colisão; não é física de corpos articulados.
- Faltas geram tiros livres; faltas dentro da área adversária geram pênaltis. Há laterais, escanteios, tiros de meta, cartões e reposições. Pênalti pendente estende o tempo necessário para a cobrança.
- Cinco substituições com até três paradas por equipe, além do intervalo. Trocas simultâneas compartilham a parada.
- Acréscimos por interrupções: substituições, lesões, gols, faltas e impedimentos. O modelo estima a duração, arredonda para minuto inteiro e tem mínimo de um minuto por tempo. O replay usa a duração retornada pelo servidor e não encerra aos 90 minutos.

As referências de posição para impedimento são os centros dos atletas. Não há análise de partes do corpo, interferência sem toque, defesa deliberada completa, mão na bola, vantagem contextual, protocolo VAR ou arbitragem integral de todas as situações IFAB. A seleção de saídas e de decisões técnicas ainda é simplificada. Portanto, esta entrega não deve ser apresentada como arbitragem completa ou simulação física realista.

Referências consultadas: [IFAB — impedimento](https://www.theifab.com/laws/2025-26/offside/) e [duração da partida](https://www.theifab.com/laws/2025-26/the-duration-of-the-match/).

## Classificação, acesso e descenso

Classificação: pontos, vitórias, saldo, gols pró, confronto direto entre dois empatados após os dois jogos, menos vermelhos, menos amarelos e sorteio com chave persistida. Para mais de dois empatados, confronto direto é ignorado. O resultado do sorteio permanece estável ao recarregar o save.

Competições permite ativar duas divisões **antes da primeira rodada**, em carreira demonstrativa. A divisão complementar acrescenta vinte clubes regionais e atletas fictícios, identificados como demonstração. Não é uma base real da Série B nem pode ser misturada a um catálogo real.

Cada divisão disputa 380 jogos. Quatro clubes descem da A. A B promove os dois primeiros diretamente; 3º×6º e 4º×5º têm ida e volta, com retorno na casa da melhor campanha. Decidem pontos, saldo agregado e melhor classificação, sem gol qualificado ou pênaltis de desempate. O clube controlado pode descer e continuar gerenciável. Partidas, tabelas, clubes antigos, playoffs e acessos ficam no histórico, com replays próprios. Este formato de 2026 fica congelado na carreira; não é uma afirmação sobre regulamentos futuros. Não há Série C nesta expansão.

Referências: [REC Série A 2026, arts. 8 e 15](https://stcbfsiteprdimgbrs.blob.core.windows.net/img-site/cdn/REC_Brasileiro_Serie_A_2026_c984f8cf05.pdf) e [REC Série B 2026, arts. 12–14](https://stcbfsiteprdimgbrs.blob.core.windows.net/img-site/cdn/REC_Brasileiro_Serie_B_2026_85d55b9f72.pdf).

## Calendário e dados reais

Desde a versão 0.10, todas as novas carreiras recebem um calendário gerado pelo jogo, assim como os anos seguintes. A tabela oficial da CBF e suas remarcações não fazem parte dos requisitos. As datas são da simulação e não impedem avançar rodadas imediatamente.

O arquivo CC0 de Openfootball permanece como referência de pesquisa, sem aplicação automática. Saves existentes preservam seus confrontos e resultados; datas futuras ausentes podem ser preenchidas sem refazer os jogos. A janela de transferências continua por rodadas. Ver [calendário por temporada](SEASON-CALENDAR.md).

## Escudos

Foram identificados arquivos e licenças individuais para dezoito clubes. Cinco originais foram incorporados: Palmeiras, Flamengo, Botafogo, Santos e São Paulo. Os demais downloads retornaram HTTP 429, com Retry-After de 120 segundos; a captura para no primeiro limite, mantém o cache e adia o restante.

Corinthians: o arquivo novo encontrado se declara obra do próprio uploader, sem comprovar origem oficial; a fotografia antiga licenciada não foi adotada como arte de escudo. Bragantino: o arquivo histórico anterior a 2020 não substitui o atual; não foi localizado um arquivo atual com direitos verificáveis. Ambos mantêm o símbolo genérico.

Manifesto e hashes: `frontend/public/data/crest-credits.json`. Atribuição dos arquivos efetivamente incluídos: `frontend/public/assets/crests/ATTRIBUTION.txt`. Arte original preservada, sem edição visual. Marcas identificam os clubes; sem alegação de endosso. Licenças de arquivos não equivalem a licença geral de marca. `fetch-reviewed-crests.py` retoma somente arquivos revisados, espaça downloads e interrompe em novo limite.

## Verificação

Builds .NET e TypeScript/Vite; regressões da simulação e API; testes específicos de impedimento, colisão, área penal, acréscimos, empréstimos de entrada/saída, reserva de vagas, multas, conservação das parcelas, vencimentos entre anos, divisões e histórico. Testes estáticos conferem calendário sem placares, hashes/licenças dos escudos e manutenção do bloqueio da base incompleta. A integração HTTP percorre 38 rodadas e a virada com replay preservado. O PostgreSQL continua exigindo validação em ambiente com banco disponível; os novos campos ficam no snapshot JSON e não acrescentam colunas relacionais.

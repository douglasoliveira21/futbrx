using Brasileirao;

public static class FootballDecisionsTests
{
    static void Check(bool yes, string text) { if (!yes) throw new Exception(text); Console.WriteLine("PASS: " + text); }
    public static void Run()
    {
        Check(FootballDecisions.Foul(false, false, false, false, true) == FootballDecisions.Sanction.None, "Falta por descuido não exige cartão");
        Check(FootballDecisions.Foul(true, false, false, false, true) == FootballDecisions.Sanction.Yellow, "Entrada temerária recebe amarelo");
        Check(FootballDecisions.Foul(false, true, false, true, true) == FootballDecisions.Sanction.Red, "Força excessiva recebe vermelho também dentro da área");
        Check(FootballDecisions.Foul(false, false, true, true, true) == FootballDecisions.Sanction.Yellow, "DOGSO com tentativa de jogar a bola e pênalti reduz sanção para amarelo");
        Check(FootballDecisions.Foul(false, false, true, true, false) == FootballDecisions.Sanction.Red, "DOGSO sem disputa da bola mantém vermelho no pênalti");
        Check(FootballDecisions.Foul(false, false, false, true, true, true) == FootballDecisions.Sanction.None, "Ataque promissor interrompido por disputa da bola com pênalti não exige amarelo");
        Check(FootballDecisions.Offside(40, 0, 30, 1, [(50,0),(38,0)]), "Ponto legal do corpo além do penúltimo adversário define posição irregular");
        Check(!FootballDecisions.Offside(38, 0, 30, 1, [(50,0),(38,0)]), "Mesma linha do penúltimo adversário é posição legal");
        Check(FootballDecisions.Offside(-40, 0, -30, -1, [(-50,0),(-38,0)]), "Impedimento considera a direção de ataque após troca de lado");
        Check(!FootballDecisions.Offside(40, 0, 42, 1, [(50,0),(38,0)]), "Atacante atrás da bola permanece em posição legal");
        Check(Math.Abs(FootballDecisions.PassingLaneDistance(5,3,0,0,10,0)-3)<.001, "Leitura do corredor de passe usa distância real ao segmento");
    }
}

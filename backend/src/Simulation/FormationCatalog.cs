namespace Brasileirao;

/// <summary>Formation templates for the unreleased tactical editor. Positions
/// are normalized to the attacking team's frame and include the goalkeeper.</summary>
public static class FormationCatalog
{
    public sealed record Template(string Id,string Label,string Category,int[] Lines,double Width=1,double Depth=1);
    public static readonly IReadOnlyList<Template> Templates = [
        new("442","4-4-2 clássico","Equilibrada",[4,4,2]),
        new("442-diamond","4-4-2 losango","Equilibrada",[4,1,2,1,2],.8),
        new("4411","4-4-1-1","Defensiva",[4,4,1,1]),
        new("433","4-3-3 clássico","Equilibrada",[4,3,3]),
        new("433-offensive","4-3-3 ofensivo","Ofensiva",[4,3,3],1.12,1.12),
        new("433-defensive","4-3-3 defensivo","Defensiva",[4,3,3],.85,.78),
        new("4231-wide","4-2-3-1 aberto","Equilibrada",[4,2,3,1],1.12),
        new("4231-narrow","4-2-3-1 estreito","Equilibrada",[4,2,3,1],.72),
        new("4141","4-1-4-1","Defensiva",[4,1,4,1]),
        new("41212","4-1-2-1-2","Equilibrada",[4,1,2,1,2]),
        new("4312","4-3-1-2","Equilibrada",[4,3,1,2]),
        new("4222","4-2-2-2","Ofensiva",[4,2,2,2]),
        new("424","4-2-4","Ofensiva",[4,2,4],1.1,1.08),
        new("451","4-5-1","Defensiva",[4,5,1],.9,.85),
        new("460","4-6-0","Alternativa",[4,6],1,.85),
        new("343","3-4-3","Ofensiva",[3,4,3]),
        new("3421","3-4-2-1","Equilibrada",[3,4,2,1]),
        new("3412","3-4-1-2","Equilibrada",[3,4,1,2]),
        new("352","3-5-2","Equilibrada",[3,5,2]),
        new("3511","3-5-1-1","Defensiva",[3,5,1,1]),
        new("3142","3-1-4-2","Equilibrada",[3,1,4,2]),
        new("3241","3-2-4-1","Ofensiva",[3,2,4,1]),
        new("3232","3-2-3-2","Ofensiva",[3,2,3,2]),
        new("3331","3-3-3-1","Equilibrada",[3,3,3,1]),
        new("334","3-3-4","Ofensiva",[3,3,4]),
        new("361","3-6-1","Defensiva",[3,6,1]),
        new("325","3-2-5","Ofensiva",[3,2,5]),
        new("3151","3-1-5-1","Equilibrada",[3,1,5,1]),
        new("532","5-3-2","Defensiva",[5,3,2]),
        new("541","5-4-1","Defensiva",[5,4,1]),
        new("523","5-2-3","Defensiva",[5,2,3]),
        new("5221","5-2-2-1","Defensiva",[5,2,2,1]),
        new("5212","5-2-1-2","Equilibrada",[5,2,1,2]),
        new("5311","5-3-1-1","Defensiva",[5,3,1,1]),
        new("5131","5-1-3-1","Defensiva",[5,1,3,1]),
        new("5122","5-1-2-2","Equilibrada",[5,1,2,2]),
        new("235","2-3-5 pirâmide","Histórica",[2,3,5]),
        new("2323","2-3-2-3 WM","Histórica",[2,3,2,3]),
        new("244","2-4-4","Histórica",[2,4,4]),
        new("253","2-5-3","Histórica",[2,5,3]),
        new("3223","3-2-2-3 WM","Histórica",[3,2,2,3])
    ];
    public static List<TacticalPoint> Positions(Template t)
    {
        if(t.Lines.Sum()!=10||t.Lines.Any(n=>n<1||n>6)||t.Width is <=0 or >1.3||t.Depth is <=0 or >1.2)throw new GameRuleException("Formação inválida.");
        var result=new List<TacticalPoint>{new(-.94,0)};
        for(int row=0;row<t.Lines.Length;row++){
            double x=(-.57+(t.Lines.Length==1?0:row*.99/(t.Lines.Length-1)))*t.Depth;
            for(int col=0;col<t.Lines[row];col++){
                double z=t.Lines[row]==1?0:(col/(double)(t.Lines[row]-1)*1.46-.73)*t.Width;
                result.Add(new(Math.Clamp(x,-.9,.9),Math.Clamp(z,-.95,.95)));
            }
        }
        return result;
    }
}

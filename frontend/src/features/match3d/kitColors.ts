const rgb=(hex:string)=>{const n=parseInt(hex.replace("#",""),16);return [(n>>16)&255,(n>>8)&255,n&255];};
export function colorDistance(a:string,b:string){const x=rgb(a),y=rgb(b);return Math.sqrt(x.reduce((sum,v,i)=>sum+(v-y[i])**2,0));}
export function matchKitColors(home:string,away:string){
 const alternatives=["#f2f2eb","#172638","#d26d28"];
 const outfield=[home,away];
 if(colorDistance(home,away)<105)outfield[1]=[...alternatives].sort((a,b)=>colorDistance(b,home)-colorDistance(a,home))[0];
 const options=["#efbd32","#28c8d0","#af72e7","#f27963","#d7e9d9"];
 const keepers:string[]=[];
 for(let i=0;i<2;i++)keepers.push([...options].sort((a,b)=>Math.min(...[...outfield,...keepers].map(c=>colorDistance(b,c)))-Math.min(...[...outfield,...keepers].map(c=>colorDistance(a,c))))[0]);
 const referee=["#f4dc35","#f28b35","#e7edf4"].sort((a,b)=>Math.min(...[...outfield,...keepers].map(c=>colorDistance(b,c)))-Math.min(...[...outfield,...keepers].map(c=>colorDistance(a,c))))[0];
 return {outfield,keepers,referee};
}

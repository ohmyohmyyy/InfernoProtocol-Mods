using System;
namespace InfernoProtocol.Renovator;
internal static class RenovatorNavigation
{
    internal static int Move(int index,int direction,int count)
    {
        if(count<=0) return 0;
        index=Math.Clamp(index,0,count-1);
        direction=direction>1?3:direction< -1?-3:direction;
        if(direction==1&&index%3==2||direction==-1&&index%3==0) return index;
        return Math.Clamp(index+direction,0,count-1);
    }
}

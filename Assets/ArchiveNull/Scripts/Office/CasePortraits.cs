using UnityEngine;
namespace ArchiveNull.UI
{
    public static class CasePortraits
    {
        private static readonly Sprite[] portraits=new Sprite[4];
        public static Sprite Get(string name)
        {
            string key=(name??"").ToUpperInvariant();
            int index=key.Contains("NICOL") ? 1 : key.Contains("SOF") ? 2 : key.Contains("VICTOR") || key.Contains("VÍCTOR") ? 3 : 0;
            if(portraits[index]!=null) return portraits[index];
            var atlas=Resources.Load<Texture2D>("CasePortraits/People");
            if(atlas==null) return null;
            int width=atlas.width/2, height=atlas.height/2;
            portraits[index]=Sprite.Create(atlas,new Rect((index%2)*width,index<2 ? height : 0,width,height),new Vector2(.5f,.5f));
            return portraits[index];
        }
    }
}

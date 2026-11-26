using System;
using System.IO;
using System.Linq;

namespace kok {
    public sealed class CollectionLogo {
        internal static readonly string[] Symbols={"auto","film","series","camera","book","folder","record","microphone","postcard"};
        internal static readonly string[] Tones={"auto","sand","sage","rose","slate"};
        public string name="",symbol="auto",tone="auto";

        internal static bool Valid(CollectionLogo logo){
            return logo!=null&&!String.IsNullOrWhiteSpace(logo.name)&&logo.name.Length<=100&&
                Symbols.Contains(logo.symbol)&&Tones.Contains(logo.tone);
        }
    }

    public partial class Library {
        internal void SaveCollectionLogo(string name,string symbol,string tone){
            var logo=new CollectionLogo{name=name,symbol=symbol,tone=tone};
            lock(gate){
                if(!CollectionLogo.Valid(logo)||!Db.collections.Contains(name))throw new InvalidDataException("分类或图标设置无效。");
                var previous=Db.collectionLogos;
                Db.collectionLogos=previous.Where(l=>l.name!=name).ToList();
                if(symbol!="auto"||tone!="auto")Db.collectionLogos.Add(logo);
                try{Save(false,Array.Empty<string>(),Array.Empty<string>());}
                catch{Db.collectionLogos=previous;throw;}
            }
        }
    }
}

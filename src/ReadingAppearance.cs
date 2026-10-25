using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace kok {
    public partial class Library {
        public static readonly string[] AppearanceKeys={"customColorsEnabled","customLightBg","customLightGlass","customDarkBg","customDarkGlass","glassOpacity","glassBlur"};
        static Dictionary<string,object> ValidateReadingAppearance(Dictionary<string,object> values){
            var result=new Dictionary<string,object>();
            foreach(var pair in values){
                object value=pair.Value;string key=pair.Key;
                if(new[]{"customLightBg","customLightGlass","customDarkBg","customDarkGlass"}.Contains(key)){
                    string color=Convert.ToString(value);if(!Regex.IsMatch(color??"","^#[0-9a-fA-F]{6}$"))throw new InvalidDataException("请选择有效的六位颜色。");result[key]=color.ToLowerInvariant();
                }else if(key=="customColorsEnabled"){
                    if(!(value is bool))throw new InvalidDataException("配色开关格式无效。");result[key]=value;
                }else if(new[]{"glassOpacity","glassBlur","readerLineHeight","readerLetterSpacing","readerParagraphSpace","readerWidth"}.Contains(key)){
                    double number;try{number=Convert.ToDouble(value);}catch{throw new InvalidDataException("阅读或外观设置数值无效。");}
                    double min=0,max=0;
                    switch(key){case "glassOpacity":min=15;max=95;break;case "glassBlur":min=0;max=48;break;case "readerLineHeight":min=1.2;max=2.8;break;case "readerLetterSpacing":min=0;max=3;break;case "readerParagraphSpace":min=0;max=2;break;case "readerWidth":min=420;max=1100;break;}
                    if(Double.IsNaN(number)||Double.IsInfinity(number)||number<min||number>max)throw new InvalidDataException("阅读或外观设置数值无效。");result[key]=number;
                }else if(key=="readerLayout"){
                    if(!new[]{"scroll","paged"}.Contains(Convert.ToString(value)))throw new InvalidDataException("阅读布局无效。");result[key]=Convert.ToString(value);
                }
            }
            return result;
        }
    }
}

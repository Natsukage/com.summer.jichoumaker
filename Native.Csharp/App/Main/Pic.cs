using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;//提供画GDI+图形的基本功能
using System.Drawing.Text;//提供画GDI+图形的高级功能
using System.Drawing.Drawing2D;//提供画高级二维，矢量图形功能
using System.Drawing.Imaging;//提供画GDI+图形的高级功能
using System.IO;
using Native.Csharp.Tool.IniConfig.Linq;
using System.Text.RegularExpressions;

namespace Native.Csharp.App.Main
{
    enum PicRepeatingType
    {
        overwrite,//直接在原图上覆写
        once,//文字在图片下方
        multiple//一行文字配一张图片
    }
    class Pic
    {
        private readonly string type = "";
        private Font font;
        private Brush brush;
        private PointF position;
        private Image srcImage = null;
        private Bitmap newImage = null;
        private Graphics graphics = null;
        private readonly StringFormat format = new StringFormat();
        private PicRepeatingType mode;
        public Pic(string type)
        {
            this.type = type;
            Initialize();
        }
        ~Pic()
        {
            srcImage.Dispose();
            newImage.Dispose();
            graphics.Dispose();
        }
        private void Initialize()
        {
            string cPath = Path.Combine(Common.AppDirectory, type + ".ini");
            if (File.Exists(cPath))
            {
                using (FileStream myStream = new FileStream(GetSrcImagePath(), FileMode.Open))
                    srcImage = Image.FromStream(myStream);

                graphics = Graphics.FromImage(srcImage);

                IniObject iObject = IniObject.Load(cPath);

                IniSection fontconfig = iObject["font"];
                string fontname = fontconfig["fontname"].ToString();
                float fontsize = fontconfig["fontsize"].ToSingle();
                font = new Font(fontname, fontsize);

                int color = Convert.ToInt32(fontconfig["rgb"].ToString(), 16);
                brush = new SolidBrush(Color.FromArgb(color));

                IniSection positionconfig = iObject["position"];
                float x = positionconfig["x"].ToSingle();
                float y = positionconfig["y"].ToSingle();
                position = new PointF(x, y);

                string alignment = positionconfig["alignment"].ToString();
                format.LineAlignment = StringAlignment.Near;
                format.Alignment = GetAlignment(alignment);

                string repeating = positionconfig["repeating"].ToString();
                mode = GetPicRepeatingType(repeating);
            }
            else
                throw new Exception(type + "的配置文件不存在！");
        }

        public static bool PicExist(string name)
        {
            string cPath = Path.Combine(Common.AppDirectory, name + ".ini");
            return File.Exists(cPath);
        }
        public static string GetRandomPic()
        {
            FileInfo[] files = new DirectoryInfo(Common.AppDirectory).GetFiles("*.ini");
            Random ran = new Random();
            string randominiName = files[ran.Next() % files.Length].Name;
            string result = Path.GetFileNameWithoutExtension(randominiName);
            return result;
        }

        public void Make(string str, int size = 0)
        {
            if (size > 0)
                font = new Font(font.FontFamily, size);

            str = Regex.Replace(str, @"\[CQ:.+?\]", "");

            switch (mode)
            {
                case PicRepeatingType.overwrite:
                    MakeOverwriteImage(str);
                    break;

                case PicRepeatingType.once:
                    MakeOnceImage(str);
                    break;

                case PicRepeatingType.multiple:
                    MakeMultipleImage(str);
                    break;

                default:
                    throw new Exception(type + "不支持的模式");
            }
        }



        private void MakeOverwriteImage(string str)
        {
            Font Afont = AdaptFontSize(str);
            newImage = new Bitmap(srcImage.Width, srcImage.Height, srcImage.PixelFormat);
            newImage.SetResolution(srcImage.HorizontalResolution, srcImage.VerticalResolution);

            graphics = Graphics.FromImage(newImage);
            graphics.Clear(Color.White);
            graphics.DrawImage(srcImage, new Point(0, 0));
            graphics.DrawString(str, Afont, brush, position, format);
            graphics.Dispose();

            return;
        }
        private void MakeOnceImage(string str)
        {
            Font Afont = AdaptFontSize(str);
            SizeF stringsize = graphics.MeasureString(str, Afont);
            newImage = new Bitmap(srcImage.Width, srcImage.Height + (int)stringsize.Height, srcImage.PixelFormat);
            newImage.SetResolution(srcImage.HorizontalResolution, srcImage.VerticalResolution);

            graphics = Graphics.FromImage(newImage);
            graphics.Clear(Color.White);
            graphics.DrawImage(srcImage, new Point(0, 0));
            graphics.DrawString(str, Afont, brush, position, format);
            graphics.Dispose();

            return;
        }
        private void MakeMultipleImage(string str)
        {
            List<string> sArray = Regex.Split(str, @"\r\n", RegexOptions.IgnoreCase).Where(s => !string.IsNullOrEmpty(s)).ToList();
            int TotalHeight = 0, currenty = 0;
            Font Afont = AdaptFontSize(str.Replace("\\n", "\n"));
            List<KeyValuePair<string, int>> StrList = new List<KeyValuePair<string, int>>();
            foreach (string i in sArray)
            {
                string istr = i.Replace("\\n", "\n");
                SizeF stringsize = graphics.MeasureString(istr, Afont);
                KeyValuePair<string, int> kvp = new KeyValuePair<string, int>(istr, (int)stringsize.Height);
                StrList.Add(kvp);
                TotalHeight += (int)stringsize.Height;
            }

            newImage = new Bitmap(srcImage.Width, srcImage.Height * sArray.Count + TotalHeight, srcImage.PixelFormat);
            newImage.SetResolution(srcImage.HorizontalResolution, srcImage.VerticalResolution);

            graphics = Graphics.FromImage(newImage);
            graphics.Clear(Color.White);

            foreach (KeyValuePair<string, int> item in StrList)
            {
                graphics.DrawImage(srcImage, new Point(0, currenty));
                graphics.DrawString(item.Key, Afont, brush, new PointF(position.X, position.Y + currenty), format);
                currenty += srcImage.Height + item.Value;
            }

            graphics.Dispose();

            return;
        }

        public string Save(long fromgroup)
        {
            string path = @"data\image\" + fromgroup.ToString() + ".jpg";
            newImage.Save(path);

            srcImage.Dispose();
            newImage.Dispose();

            return fromgroup.ToString() + ".jpg";
        }
        private Font AdaptFontSize(string str)   //尝试找出尽可能匹配图片宽度的字号。
        {
            Font Afont = font;
            try
            {
                while (Afont.Size > 5)  //封底5号字（不会继续再缩小了）
                {
                    SizeF stringsize = graphics.MeasureString(str, Afont);
                    if (stringsize.Width < srcImage.Width)
                        break;
                    Afont = new Font(Afont.FontFamily, Afont.Size - 1);
                }
            }
            catch (Exception ex)
            {
                Common.CqApi.AddLoger(Sdk.Cqp.Enum.LogerLevel.Error, "决定字号", ex.Message);
                return font;
            }
            return Afont;
        }
        private StringAlignment GetAlignment(string alignment)
        {
            switch (alignment)
            {
                case "1":
                case "l":
                case "n":
                    return StringAlignment.Near;
                case "2":
                case "c":
                    return StringAlignment.Center;
                case "3":
                case "f":
                case "r":
                    return StringAlignment.Far;
                default:
                    throw new Exception(type + "的对齐方式不合法！");
            }
        }

        private PicRepeatingType GetPicRepeatingType(string RepeatingType)
        {
            switch (RepeatingType)
            {
                case "0":
                case "once":
                    return PicRepeatingType.once;
                case "1":
                case "overwrite":
                    return PicRepeatingType.overwrite;
                case "2":
                case "multiple":
                    return PicRepeatingType.multiple;
                default:
                    throw new Exception(type + "图片的描绘方式不合法！");
            }
        }

        private string GetSrcImagePath()
        {
            List<string> exts = new List<string>() { ".jpg", ".png", ".bmp" };
            foreach (string ext in exts)
                if (File.Exists(Path.Combine(Common.AppDirectory, type + ext)))
                    return Path.Combine(Common.AppDirectory, type + ext);

            throw new Exception(type + "的图片不存在！");
        }
    }
}

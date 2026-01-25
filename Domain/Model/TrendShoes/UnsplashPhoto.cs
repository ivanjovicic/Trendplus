using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Domain.Model.TrendShoes
{
    public class UnsplashPhoto
    {
        public string id { get; set; }
        public UnsplashUrls urls { get; set; }
        public string alt_description { get; set; }
        public UnsplashUser user { get; set; }
        public UnsplashLinks links { get; set; }
    }

    public class UnsplashUser
    {
        public string name { get; set; }
        public string username { get; set; }
        public UnsplashUserLinks links { get; set; }
    }

    public class UnsplashUserLinks
    {
        public string html { get; set; }
    }

    public class UnsplashLinks
    {
        public string download_location { get; set; }
    }
}

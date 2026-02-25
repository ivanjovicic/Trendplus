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
        public string id { get; set; } = string.Empty;
        public UnsplashUrls urls { get; set; } = new();
        public string alt_description { get; set; } = string.Empty;
        public UnsplashUser user { get; set; } = new();
        public UnsplashLinks links { get; set; } = new();
    }

    public class UnsplashUser
    {
        public string name { get; set; } = string.Empty;
        public string username { get; set; } = string.Empty;
        public UnsplashUserLinks links { get; set; } = new();
    }

    public class UnsplashUserLinks
    {
        public string html { get; set; } = string.Empty;
    }

    public class UnsplashLinks
    {
        public string download_location { get; set; } = string.Empty;
    }
}

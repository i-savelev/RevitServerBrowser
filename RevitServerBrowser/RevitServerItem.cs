using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitServerBrowser
{
    public class RevitServerItem
    {
        public string Name { get; private set; }
        public string ItemType { get; private set; }
        public string Path { get; private set; }

        public RevitServerItem(string name, string itemType, string path)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentNullException("name");
            if (string.IsNullOrEmpty(itemType)) throw new ArgumentNullException("itemType");
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException("path");

            Name = name;
            ItemType = itemType;
            Path = path;
        }

        public bool IsFolder { get { return string.Equals(ItemType, "Folder", StringComparison.OrdinalIgnoreCase); } }
        public bool IsModel { get { return string.Equals(ItemType, "Model", StringComparison.OrdinalIgnoreCase); } }
    }
}

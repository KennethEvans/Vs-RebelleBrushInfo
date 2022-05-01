using MetadataExtractor;
using System.Collections.Generic;

namespace RebelleBrushInfo {
    class Metadata {
        public string DirectoryName { get; }
        public string Name { get; }
        public string Text { get; }

        public Metadata(string directoryName, string name, string text) {
            DirectoryName = directoryName;
            Name = name;
            Text = text;
        }
    }
}

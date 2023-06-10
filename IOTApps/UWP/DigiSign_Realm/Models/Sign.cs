using System;
using System.Collections.Generic;
using System.Text;
using Realms;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DigiSign_Realm.Models
{
    class Sign
    {
        [PrimaryKey]
        [BsonElement("_id")]
        public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

        [BsonElement("order")]
        public int Order { get; set; } = 0;

        [BsonElement("duration")]
        public int Duration { get; set; } = 0;

        [BsonElement("type")]
        [Required]
        public string Type { get; set; }

        [BsonElement("feed")]
        [Required]
        public string Feed { get; set; }

        [BsonElement("name")]
        [Required]
        public string Name { get; set; }

        [BsonElement("text")]
        public string Text { get; set; }

        [BsonElement("uri")]
        public string URI { get; set; }
        [BsonElement("format")]
        public string TextFormat { get; set; }

        [BsonElement("icon")]
        public string Icon { get; set; }

        [BsonElement("updated")]
        public DateTime Updated { get; set; }

        [BsonElement("created")]
        public DateTime Created { get; set; }

        [BsonElement("_pk")]
        public string _pk { get; set; }
    }
}

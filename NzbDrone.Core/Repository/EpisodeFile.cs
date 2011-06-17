﻿using System;
using System.Collections.Generic;
using NzbDrone.Core.Repository.Quality;
using PetaPoco;

namespace NzbDrone.Core.Repository
{
    [TableName("EpisodeFiles")]
    [PrimaryKey("EpisodeFileId", autoIncrement = true)]
    public class EpisodeFile
    {
        public virtual int EpisodeFileId { get; set; }

        public virtual int SeriesId { get; set; }
        public virtual int SeasonNumber { get; set; }
        public string Path { get; set; }
        public QualityTypes Quality { get; set; }
        public bool Proper { get; set; }
        public long Size { get; set; }
        public DateTime DateAdded { get; set; }

        [Ignore]
        public virtual IList<Episode> Episodes { get; set; }

        [Ignore]
        public virtual Series Series { get; set; }
    }
}
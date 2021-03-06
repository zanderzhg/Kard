﻿using Kard.Domain.Entities.Auditing;
using System;

namespace Kard.Core.Entities
{
    //Table:cover
    public class CoverEntity : LastModificationAuditedEntity, ILastModificationAuditedEntity
    {
        public CoverEntity()
        { 
        }

        public int Id { get; set; }
        public DateTime ShowDate { get; set; }
        public long MediaId { get; set; }

        public MediaEntity Media{ get; set; }
    }

}
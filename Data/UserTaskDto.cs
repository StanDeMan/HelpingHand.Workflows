using System;

namespace GingerMintSoft.WorkFlows.Data
{
    public class UserTaskDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? HelperId { get; set; }
        public Guid? RatingRelationId { get; set; }
        public Guid? AddressId { get; set; }
        public int? TaskTypeId { get; set; }
        public int TaskStatusTypeId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime? Scheduled { get; set; }
        public decimal? Price { get; set; } 
        public bool Payed { get; set; }
    }
}
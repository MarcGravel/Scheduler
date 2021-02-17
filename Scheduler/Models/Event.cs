using Microsoft.AspNetCore.Authorization;
using Scheduler.Areas.Identity.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace Scheduler.Models
{
    public class Event
    {
        [Required]
        public int EventId { get; set; }

        [ForeignKey("UserId")]
        public virtual SchedulerUser SchedulerUser { get; set; }

        [MaxLength(50)]
        public string EventCreator { get; set; }

        public List<string> EventMembers { get; set; }
        public List<string> DeclinedMembers { get; set; }

        [Required]
        [MaxLength(100)]
        public string Subject { get; set; }

        [MaxLength(400)]
        public string Description { get; set; }

        [Required]
        public string Location { get; set; }

        [Required]
        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        [Required]
        public bool IsFullDay { get; set; }
    }
}

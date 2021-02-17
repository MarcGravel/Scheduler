using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Scheduler.Areas.Identity.Data;
using Scheduler.Data;
using Scheduler.Models;

namespace Scheduler.Controllers
{
    public class EventsController : Controller
    {
        private readonly SchedulerDbContext _context;
        private readonly UserManager<SchedulerUser> _userManager;

        public EventsController(SchedulerDbContext context, UserManager<SchedulerUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Events
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var userName = user.UserName;

            // View current users event list.
            var queryEventMembers = _context.Events
                .AsNoTracking();
            List<Event> eventList = new List<Event>();
            foreach (var q in queryEventMembers)
            {
                if (q.EventMembers.Contains(userName) || q.EventCreator.Contains(userName))
                {
                    eventList.Add(q);
                } 
            }

            // View event members and creator real names
            var allUsers = _userManager.Users
                .AsNoTracking();
            foreach (var a in allUsers)
            {
                foreach (var e in eventList)
                {
                    if (e.EventMembers.Contains(a.UserName))
                    {
                        var index = e.EventMembers.IndexOf(a.UserName);
                        e.EventMembers[index] = a.FirstName + " " + a.LastName;
                    }

                    if (e.EventCreator.Contains(a.UserName))
                    {
                        e.EventCreator = a.FirstName + " " + a.LastName;
                    }
                }
            }

            // Avoid duplicate event in list.
            var distinctEventList = eventList.Distinct();
            return View(distinctEventList);
        }

        // GET: Events/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .FirstOrDefaultAsync(m => m.EventId == id);

            // View event members and creator real names
            var allUsers = _userManager.Users
                .AsNoTracking();
            foreach (var a in allUsers)
            {
                if (@event.EventMembers.Contains(a.UserName))
                {
                    var index = @event.EventMembers.IndexOf(a.UserName);
                    @event.EventMembers[index] = a.FirstName + " " + a.LastName;
                }

                if (@event.EventCreator.Contains(a.UserName))
                {
                    @event.EventCreator = a.FirstName + " " + a.LastName;
                }
            }

            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // GET: Events/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Events/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("EventId,UserId,EventCreator,EventMembers,DeclinedMembers,Subject,Description,Location,StartTime,EndTime,IsFullDay")] Event @event)
        {
            if (ModelState.IsValid)
            {
                @event.SchedulerUser = await _userManager.GetUserAsync(HttpContext.User); //This also sets the UserId for the event.

                var userName = @event.SchedulerUser.UserName;
                @event.EventCreator = userName;
                @event.DeclinedMembers = new List<string>(); // Initialize declinded members list.

                //Lowercase all members
                @event.EventMembers = @event.EventMembers.ConvertAll(d => d.ToLower());

                //Check for event creator in member input
                foreach (var e in @event.EventMembers)
                {
                    if (e == @event.EventCreator)
                    {
                        TempData["CreatorNotMember"] = "You cannot put yourself as a Member. Don't worry, you are definitly a part of the event you created.";
                        return View();
                    }
                }

                //Check for duplicate members into member input
                var duplicateMembers = from x in @event.EventMembers
                                       group x by x into grouped
                                       where grouped.Count() > 1
                                       select grouped.Key;
                List<string> duplicateMemberList = duplicateMembers.ToList();
                if (duplicateMemberList.Count >= 1)
                {
                    TempData["DuplicateMember"] = "You added " + String.Join("& ", duplicateMemberList) + " twice. You cannot do that.";
                    return View();
                }
                

                //Check if added members have accounts. Only registered users can be added as Event Members.
                var allUsers = _userManager.Users
                .AsNoTracking();
                List<string> allUserNames = new List<string>(allUsers.Select(u => u.UserName));
                List<string> unregisteredUser = new List<string>(@event.EventMembers.Except(allUserNames));
                if (unregisteredUser.Count == 1)
                {
                    TempData["UserNotRegistered"] = String.Join(",", unregisteredUser) + " is not registered with Scheduler. Please enter a valid e-mail address.";
                    return View();
                }
                if (unregisteredUser.Count > 1)
                {
                    TempData["MultiUsersNotRegistered"] = String.Join(", ", unregisteredUser) + " are not registered with Scheduler. Please enter a valid e-mail address.";
                    return View();
                }

                _context.Add(@event);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(@event);
        }

        // GET: Events/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events.FindAsync(id);

            if (@event == null)
            {
                return NotFound();
            }
            return View(@event);
        }

        // POST: Events/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("EventId,UserId,EventCreator,EventMembers,DeclinedMembers,Subject,Description,Location,StartTime,EndTime,IsFullDay")] Event @event)
        {
            if (id != @event.EventId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    @event.SchedulerUser = await _userManager.GetUserAsync(HttpContext.User); // Find current User

                    //Lowercase all members
                    @event.EventMembers = @event.EventMembers.ConvertAll(d => d.ToLower());

                    // Checks current event. Initialize new list for updated Declined members. 
                    // Add current members that have already declined event to new declined list.
                    // This removes declinded member count for event members that have been removed during edit.
                    // Not required for event creator as they cannot be edited. Once declined it is final (shown as +1 in delete confirmed).
                    var currentEvent = await _context.Events
                        .AsNoTracking()
                        .FirstOrDefaultAsync(e => e.EventId == id);    
                    List<string> currentDeclindedMembers = new List<string>();
                    foreach (var e in @event.EventMembers)
                    {
                        foreach (var d in currentEvent.DeclinedMembers)
                        {
                            if (e == d)
                            {
                                currentDeclindedMembers.Add(e);
                            }
                        }
                    }
                    @event.DeclinedMembers = currentDeclindedMembers;

                    _context.Update(@event);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!EventExists(@event.EventId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(@event);
        }

        // GET: Events/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {

            if (id == null)
            {
                return NotFound();
            }

            var @event = await _context.Events
                .FirstOrDefaultAsync(m => m.EventId == id);
            if (@event == null)
            {
                return NotFound();
            }

            return View(@event);
        }

        // POST: Events/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var @event = await _context.Events.FindAsync(id);
            var user = await _userManager.GetUserAsync(HttpContext.User);
            var userName = user.UserName;

            // Adds current user to DeclinedMembers list if users first time declining current event
            foreach (var e in @event.EventMembers)
            {
                if (!@event.DeclinedMembers.Contains(userName))
                {
                    @event.DeclinedMembers.Add(userName);
                }
            }

            // The +1 is the count for EventCreator who also must decline before event is removed. 
            //If all members + creator decline, event is removed.
            if (@event.EventMembers.Count + 1 == @event.DeclinedMembers.Count)
            {
                _context.Events.Remove(@event);
            }

            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool EventExists(int id)
        {
            return _context.Events.Any(e => e.EventId == id);
        }
    }
}

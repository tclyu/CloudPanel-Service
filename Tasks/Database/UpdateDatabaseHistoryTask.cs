﻿using CPService.Database;
using log4net;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CPService.Tasks.Database
{
    [DisallowConcurrentExecution]
    public class UpdateDatabaseHistoryTask : IJob
    {
        private static readonly ILog logger = LogManager.GetLogger("UpdateDatabaseHistoryTask");

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                using (CloudPanelDbContext db = new CloudPanelDbContext(Config.ServiceSettings.SqlConnectionString))
                {

                    // Get a list of ALL companies
                    var companies = db.Companies.Where(x => x.IsReseller != true)
                                                .Select(x => new
                                                {
                                                    ResellerCode = x.ResellerCode,
                                                    CompanyCode = x.CompanyCode,
                                                    CompanyName = x.CompanyName
                                                }).ToList();

                    // Set our date and time when we started this task
                    DateTime now = DateTime.Now;

                    // Go through all companies getting the latest values
                    companies.ForEach(x =>
                    {
                        // Query all users
                        List<Users> users = db.Users.Where(a => a.CompanyCode == x.CompanyCode)
                                                .ToList();

                        // See if we have any in Citrix
                        List<int> userIds = users.Select(a => a.ID).ToList();
                        int citrixUsers = db.CitrixUserToDesktopGroup.Where(a => userIds.Contains(a.UserRefDesktopGroupId))
                                                                     .Select(a => a.UserRefDesktopGroupId)
                                                                     .Distinct()
                                                                     .Count();

                        Statistics newStatistic = new Statistics();
                        newStatistic.UserCount = users.Count;
                        newStatistic.MailboxCount = users.Where(a => a.MailboxPlan > 0).Count();
                        newStatistic.CitrixCount = citrixUsers;
                        newStatistic.ResellerCode = x.ResellerCode;
                        newStatistic.CompanyCode = x.CompanyCode;
                        newStatistic.Retrieved = now;

                        db.Statistics.InsertOnSubmit(newStatistic);
                    });

                    // Save changes to the database
                    db.SubmitChanges();
                }
            }
            catch (Exception ex)
            {
                logger.ErrorFormat("Error getting history statistics: {0}", ex.ToString());
            }
        }
    }
}

﻿using DraconiusGoGUI.AccountScheduler;
using DraconiusGoGUI.Enums;
using System;
using System.Threading.Tasks;

namespace DraconiusGoGUI.DracoManager
{
    public partial class Manager
    {
        private DateTime _lastTrackerUpdate = new DateTime();

        public void AddSchedulerEvent()
        {
            if (AccountScheduler != null)
            {
                AccountScheduler.OnSchedule -= Scheduler_OnSchedule;
                AccountScheduler.OnSchedule += Scheduler_OnSchedule;
            }
        }

        public void AddScheduler(Scheduler scheduler)
        {
            if(AccountScheduler != null)
            {
                RemoveScheduler();
            }

            scheduler.OnSchedule += Scheduler_OnSchedule;

            AccountScheduler = scheduler;
        }

        public void RemoveScheduler()
        {
            if(AccountScheduler != null)
            {
                AccountScheduler.OnSchedule -= Scheduler_OnSchedule;
            }

            AccountScheduler = null;
        }

        private async void Scheduler_OnSchedule(object sender, SchedulerEventArgs e)
        {
            DateTime currentTime = Tracker.GetCurrentHourDateTime();

            if (currentTime != _lastTrackerUpdate)
            {
                _lastTrackerUpdate = currentTime;

                Tracker.CalculatedTrackingHours();
            }

            Scheduler scheduler = e.Scheduler;

            //Allowing event to be called to update tracked hours when not running
            if(!scheduler.Enabled)
            {
                return;
            }

            //Don't auto start when max level is hit
            if(UserSettings.MaxLevel != 0 && Level >= UserSettings.MaxLevel)
            {
                return;
            }

            //Should not start with these states
            if(AccountState == AccountState.PermanentBan || AccountState == AccountState.NotVerified)
            {
                return;
            }

            int delay = 0;

            lock(_rand)
            {
                delay = _rand.Next(0, 45000);
            }

            if (e.Scheduler.WithinTime())
            {
                if (State == Enums.BotState.Stopped)
                {
                    //Only auto start when both are below min values
                    //Otherwise we'll get constant start/stops
                    if ((CreatureCaught <= scheduler.CreatureLimiter.Min || scheduler.CreatureLimiter.Option == SchedulerOption.Nothing) &&
                        (PokestopsFarmed <= scheduler.PokeStoplimiter.Min || scheduler.PokeStoplimiter.Option == SchedulerOption.Nothing))
                    {
                        LogCaller(new LoggerEventArgs(String.Format("Auto starting (schedule) in {0} seconds...", delay/1000), LoggerTypes.Debug));

                        await Task.Delay(delay);

                        Start();
                    }
                }
            }
            else
            {
                if (State != Enums.BotState.Stopping && State != Enums.BotState.Stopped)
                {
                    LogCaller(new LoggerEventArgs("Auto stopping (schedule) ...", LoggerTypes.Debug));
                    Stop();
                }
            }

            if (!IsRunning)
            {
                return;
            }

            //Master stop
            if (scheduler.MasterOption == SchedulerOption.StartStop)
            {
                if (State != BotState.Stopping && State != BotState.Stopped)
                {
                    if (CreatureCaught >= scheduler.CreatureLimiter.Max && PokestopsFarmed >= scheduler.PokeStoplimiter.Max)
                    {
                        LogCaller(new LoggerEventArgs("Max Creature and pokestop limit reached. Stopping", LoggerTypes.Debug));
                        Stop();

                        return;
                    }
                }
            }

            //Creature
            if (scheduler.CreatureLimiter.Option != SchedulerOption.Nothing)
            {
                if (CreatureCaught >= scheduler.CreatureLimiter.Max)
                {

                    switch (scheduler.CreatureLimiter.Option)
                    {
                        case SchedulerOption.DisableEnable: //No extra checks
                            if(UserSettings.CatchCreature)
                            {
                                LogCaller(new LoggerEventArgs("Max Creature limit reached. Disabling setting...", LoggerTypes.Debug));
                                UserSettings.CatchCreature = false;
                            }
                            break;
                        case SchedulerOption.StartStop: //Just stop it
                            LogCaller(new LoggerEventArgs("Max Creature limit reached. Stopping bot...", LoggerTypes.Debug));
                            Stop();
                            break;
                    }
                }
                else if (CreatureCaught <= scheduler.CreatureLimiter.Min)
                {
                    switch (scheduler.CreatureLimiter.Option)
                    {
                        case SchedulerOption.DisableEnable: //No extra checks
                            if (!UserSettings.CatchCreature)
                            {
                                LogCaller(new LoggerEventArgs("Min Creature limit reached. Enabling catching...", LoggerTypes.Debug));
                                UserSettings.CatchCreature = true;
                            }
                            break;
                        case SchedulerOption.StartStop: //Start only if pokestop is disabled/nothing or pokestops below threshold
                            if (scheduler.PokeStoplimiter.Option != SchedulerOption.StartStop ||
                                PokestopsFarmed <= scheduler.PokeStoplimiter.Min)
                            {
                                if (State == BotState.Stopped)
                                {
                                    LogCaller(new LoggerEventArgs(String.Format("Min Creature limit reached. Starting in {0} seconds", delay/1000), LoggerTypes.Debug));

                                    await Task.Delay(delay);

                                    Start();
                                }
                            }
                            break;
                    }
                }
            }

            //Pokestops
            if (scheduler.PokeStoplimiter.Option != SchedulerOption.Nothing)
            {
                if (PokestopsFarmed >= scheduler.PokeStoplimiter.Max)
                {
                    switch (scheduler.PokeStoplimiter.Option)
                    {
                        case SchedulerOption.DisableEnable: //No extra checks
                            if (UserSettings.SearchFortBelowPercent != 0)
                            {
                                LogCaller(new LoggerEventArgs("Max pokestop limit reached. Disabling...", LoggerTypes.Debug));
                                UserSettings.SearchFortBelowPercent = 0;
                            }
                            break;
                        case SchedulerOption.StartStop: //Just stop it
                            LogCaller(new LoggerEventArgs("Max pokestop limit reached. Stopping ...", LoggerTypes.Debug));
                            Stop();
                            break;
                    }
                }
                else if (PokestopsFarmed <= scheduler.PokeStoplimiter.Min)
                {
                    switch (scheduler.PokeStoplimiter.Option)
                    {
                        case SchedulerOption.DisableEnable: //No extra checks
                            if (UserSettings.SearchFortBelowPercent != 1000)
                            {
                                LogCaller(new LoggerEventArgs("Min pokestop limit reached. Enabling ...", LoggerTypes.Debug));
                                UserSettings.SearchFortBelowPercent = 1000;
                            }
                            break;
                        case SchedulerOption.StartStop: //Start only if Creature is disabled/nothing or Creature caught below threshold
                            if (scheduler.CreatureLimiter.Option != SchedulerOption.StartStop ||
                                CreatureCaught <= scheduler.CreatureLimiter.Min)
                            {
                                if (State == BotState.Stopped)
                                {
                                    LogCaller(new LoggerEventArgs(String.Format("Min pokestop limit reached. Starting in {0} seconds", delay/1000), LoggerTypes.Debug));

                                    await Task.Delay(delay);

                                    Start();
                                }
                            }
                            break;
                    }
                }
            }
        }
    }
}

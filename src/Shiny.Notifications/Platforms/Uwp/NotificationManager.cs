﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using Windows.ApplicationModel.Background;
using Shiny.Infrastructure;


namespace Shiny.Notifications
{
    public class NotificationManager : INotificationManager, IPersistentNotificationManagerExtension
    {
        readonly ShinyCoreServices services;
        readonly BadgeUpdater badgeUpdater;


        public NotificationManager(ShinyCoreServices services)
        {
            this.badgeUpdater = BadgeUpdateManager.CreateBadgeUpdaterForApplication();
            this.services = services;
        }


        public void Start()
        {
            if (this.services.Services.GetService(typeof(INotificationDelegate)) != null)
            {
                UwpPlatform.RegisterBackground<NotificationBackgroundTaskProcessor>(
                    builder => builder.SetTrigger(new UserNotificationChangedTrigger(NotificationKinds.Toast))
                );
            }
        }


        public IPersistentNotification Create(Notification notification)
        {
            //AdaptiveProgressBarValue.Indeterminate;
            var content = this.CreateContent(notification);
            content.Visual.BindingGeneric.Children.Add(new AdaptiveProgressBar
            {
                //    Title = "Weekly playlist",
                //    Value = new BindableProgressBarValue("progressValue"),
                //    ValueStringOverride = new BindableString("progressValueString"),
                //    Status = new BindableString("progressStatus")
            });
            notification.ScheduleDate = null;
            var pnotification = new UwpPersistentNotification(notification);
            //ToastNotificationManager.CreateToastNotifier().Show(native);

            return pnotification;
        }


        public Task<AccessState> RequestAccess()
            => this.services.Jobs.RequestAccess();


        public async Task Send(Notification notification)
        {
            // create the notification to validate it
            if (notification.Id == 0)
                notification.Id = this.services.Settings.IncrementValue("NotificationId");

            var native = this.CreateNativeNotification(notification);
            if (notification.ScheduleDate != null && notification.ScheduleDate > DateTimeOffset.UtcNow)
            {
                await this.services.Repository.Set(notification.Id.ToString(), notification);
                return;
            }

            ToastNotificationManager.CreateToastNotifier().Show(native);
            if (notification.BadgeCount != null)
                this.Badge = notification.BadgeCount.Value;

            await this.services
                .Services
                .SafeResolveAndExecute<INotificationDelegate>(
                    x => x.OnReceived(notification)
                );
        }


        public async Task<IEnumerable<Notification>> GetPending()
            => await this.services.Repository.GetAll<Notification>();


        public async Task Clear()
        {
            ToastNotificationManager.History.Clear();
            await this.services.Repository.Clear<Notification>();
        }


        public async Task Cancel(int id)
        {
            ToastNotificationManager.History.Remove(id.ToString());
            await this.services.Repository.Remove<Notification>(id.ToString());
        }


        const string BADGE_KEY = "ShinyNotificationBadge";
        public int Badge
        {
            get => this.services.Settings.Get(BADGE_KEY, 0);
            set
            {
                var badge = new BadgeNumericContent((uint)value);
                this.badgeUpdater.Update(new BadgeNotification(badge.GetXml()));
                this.services.Settings.Set(BADGE_KEY, value);
            }
        }


        protected virtual ToastNotification CreateNativeNotification(Notification notification)
        {
            var toastContent = this.CreateContent(notification);
            var native = new ToastNotification(toastContent.GetXml())
            {
                Tag = notification.Id.ToString(),
                Group = notification.Channel
            };
            return native;
        }



        public async Task SetChannels(params Channel[] channels)
        {
            await this.services.Repository.DeleteAllChannels();
            foreach (var channel in channels)
                await this.services.Repository.SetChannel(channel);
        }


        public Task<IList<Channel>> GetChannels() => this.services.Repository.GetChannels();


        protected async Task TrySetChannel(Notification notification, ToastContent content)
        {
            //if (!notification.Category.IsEmpty())
            //{
            //    var category = this.registeredCategories.FirstOrDefault(x => x.Identifier.Equals(notification.Category));

            //    var nativeActions = new ToastActionsCustom();

            //    foreach (var action in category.Actions)
            //    {
            //        switch (action.ActionType)
            //        {
            //            case NotificationActionType.OpenApp:
            //                nativeActions.Buttons.Add(new ToastButton(action.Title, action.Identifier)
            //                {
            //                    //ActivationType = ToastActivationType.Foreground
            //                    ActivationType = ToastActivationType.Background
            //                });
            //                break;

            //            case NotificationActionType.None:
            //            case NotificationActionType.Destructive:
            //                nativeActions.Buttons.Add(new ToastButton(action.Title, action.Identifier)
            //                {
            //                    ActivationType = ToastActivationType.Background
            //                });
            //                break;

            //            case NotificationActionType.TextReply:
            //                nativeActions.Inputs.Add(new ToastTextBox(action.Identifier)
            //                {
            //                    Title = notification.Title
            //                    //DefaultInput = "",
            //                    //PlaceholderContent = ""
            //                });
            //                break;
            //        }
            //    }
            //    toastContent.Actions = nativeActions;
            //}
        }


        protected virtual ToastContent CreateContent(Notification notification)
        {
            var content = new ToastContent
            {
                Duration = ToastDuration.Short,
                //Launch = notification.Payload,
                //ActivationType = ToastActivationType.Background,
                ActivationType = ToastActivationType.Foreground,
                Visual = new ToastVisual
                {
                    BindingGeneric = new ToastBindingGeneric
                    {
                        Children =
                            {
                                new AdaptiveText
                                {
                                    Text = notification.Title
                                },
                                new AdaptiveText
                                {
                                    Text = notification.Message
                                }
                            }
                    }
                }
            };
            //if (!Notification.CustomSoundFilePath.IsEmpty())
            //    toastContent.Audio = new ToastAudio { Src = new Uri(Notification.CustomSoundFilePath) };
            return content;
        }



        //string BuildSoundPath(string sound)
        //{
        //    var ext = Path.GetExtension(sound);
        //    if (String.IsNullOrWhiteSpace(ext))
        //        sound += ".mp4";

        //    if (sound.StartsWith("ms-appx://"))
        //        sound = "ms-appx://" + sound;

        //    return sound;
        //}
    }
}
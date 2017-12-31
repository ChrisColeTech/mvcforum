﻿namespace MvcForum.Web.Controllers
{
    using System;
    using System.Web.Mvc;
    using Core.Constants;
    using Core.ExtensionMethods;
    using Core.Interfaces.Services;
    using Core.Interfaces.UnitOfWork;
    using ViewModels;
    using ViewModels.Moderate;

    [Authorize]
    public partial class ModerateController : BaseController
    {
        private readonly ICategoryService _categoryService;
        private readonly IPostService _postService;
        private readonly ITopicService _topicService;

        public ModerateController(ILoggingService loggingService, IUnitOfWorkManager unitOfWorkManager,
            IMembershipService membershipService,
            ILocalizationService localizationService, IRoleService roleService, ISettingsService settingsService,
            IPostService postService,
            ITopicService topicService, ICategoryService categoryService, ICacheService cacheService)
            : base(loggingService, unitOfWorkManager, membershipService, localizationService, roleService,
                settingsService, cacheService)
        {
            _postService = postService;
            _topicService = topicService;
            _categoryService = categoryService;
        }

        public ActionResult Index()
        {
            using (UnitOfWorkManager.NewUnitOfWork())
            {
                var loggedOnReadOnlyUser = User.GetMembershipUser(MembershipService);
                var loggedOnUsersRole = loggedOnReadOnlyUser.GetRole(RoleService);

                // Show both pending topics and also pending posts
                // Use ajax for paging too
                var allowedCategories = _categoryService.GetAllowedCategories(loggedOnUsersRole);
                var viewModel = new ModerateViewModel
                {
                    Posts = _postService.GetPendingPosts(allowedCategories, loggedOnUsersRole),
                    Topics = _topicService.GetPendingTopics(allowedCategories, loggedOnUsersRole)
                };
                return View(viewModel); 
            }
        }

        [HttpPost]
        public ActionResult ModerateTopic(ModerateActionViewModel viewModel)
        {
            using (var unitOfWork = UnitOfWorkManager.NewUnitOfWork())
            {
                try
                {
                    var loggedOnReadOnlyUser = User.GetMembershipUser(MembershipService);
                    var loggedOnUsersRole = loggedOnReadOnlyUser.GetRole(RoleService);

                    var topic = _topicService.Get(viewModel.TopicId);
                    var permissions = RoleService.GetPermissions(topic.Category, loggedOnUsersRole);

                    // Is this user allowed to moderate - We use EditPosts for now until we change the permissions system
                    if (!permissions[SiteConstants.Instance.PermissionEditPosts].IsTicked)
                    {
                        return Content(LocalizationService.GetResourceString("Errors.NoPermission"));
                    }

                    if (viewModel.IsApproved)
                    {
                        topic.Pending = false;
                    }
                    else
                    {
                        _topicService.Delete(topic, unitOfWork);
                    }

                    unitOfWork.Commit();
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    LoggingService.Error(ex);
                    return Content(ex.Message);
                }
            }

            return Content("allgood");
        }

        [HttpPost]
        public ActionResult ModeratePost(ModerateActionViewModel viewModel)
        {
            using (var unitOfWork = UnitOfWorkManager.NewUnitOfWork())
            {
                try
                {
                    var loggedOnReadOnlyUser = User.GetMembershipUser(MembershipService);
                    var loggedOnUsersRole = loggedOnReadOnlyUser.GetRole(RoleService);

                    var post = _postService.Get(viewModel.PostId);
                    var permissions = RoleService.GetPermissions(post.Topic.Category, loggedOnUsersRole);
                    if (!permissions[SiteConstants.Instance.PermissionEditPosts].IsTicked)
                    {
                        return Content(LocalizationService.GetResourceString("Errors.NoPermission"));
                    }

                    if (viewModel.IsApproved)
                    {
                        post.Pending = false;
                    }
                    else
                    {
                        _postService.Delete(post, unitOfWork, false);
                    }

                    unitOfWork.Commit();
                }
                catch (Exception ex)
                {
                    unitOfWork.Rollback();
                    LoggingService.Error(ex);
                    return Content(ex.Message);
                }
            }

            return Content("allgood");
        }
    }
}
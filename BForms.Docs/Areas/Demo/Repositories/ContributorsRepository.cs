﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Web;
using BForms.Docs.Areas.Demo.Mock;
using BForms.Docs.Areas.Demo.Models;
using BForms.Docs.Resources;
using BForms.Grid;
using BForms.Models;
using BForms.Utilities;
using BForms.Docs.Areas.Demo.Helpers;
using WebGrease.Css.Extensions;

namespace BForms.Docs.Areas.Demo.Repositories
{
    public class ContributorsRepository : BsBaseGridRepository<Contributor, ContributorRowModel>
    {
        #region Properties and Constructor
        private BFormsContext db;

        public BsGridRepositorySettings<ContributorSearchModel> Settings
        {
            get
            {
                return settings as BsGridRepositorySettings<ContributorSearchModel>;
            }
            set
            {
                settings = value;
            }
        }

        public ContributorsRepository(BFormsContext _db)
        {
            db = _db;
        }
        #endregion

        #region Mappers
        public Func<Contributor, ContributorRowModel> MapContributor_ContributorRowModel = x =>
            new ContributorRowModel
            {
                Id = x.Id,
                Enabled = x.Enabled,
                Name = x.FirstName + " " + x.LastName,
                Role = x.Role,
                Contributions = x.Contributions,
                StartDate = x.StartDate,
            };

        public Func<Contributor, ContributorDetailsReadonly> MapContributor_ContributorDetailsModel = x =>
            new ContributorDetailsReadonly
            {
                Id = x.Id,
                Enabled = x.Enabled,
                Identity = new ContributorIdentityModel
                {
                    FirstName = x.FirstName,
                    LastName = x.LastName,
                    Url = x.Url,
                    Country = x.Country,
                    Age = x.Age
                },
                ProjectRelated = new ContributorProjectRelatedModel
                {
                    Languages = x.Languages,
                    Role = x.Role,
                    ContributorSince = x.StartDate,
                    Contributions = x.Contributions,
                }
            };

        public Func<Contributor, ContributorDetailsEditable> MapContributor_ContributorDetailsEditable = x => new ContributorDetailsEditable
        {
            Id = x.Id,
            Identity = new ContributorIdentityEditableModel
            {
                FirstName = x.FirstName,
                LastName = x.LastName,
                Url = x.Url,
                CountriesList = Lists.AllCounties<string>(false)
            },
            ProjectRelated = new ContributorProjectEditableRelatedModel
            {
                Contributions = x.Contributions,
                LanguagesList = Lists.AllLanguages<List<string>>(),
                StartDate = new BsDateTime()
                {
                    DateValue = x.StartDate
                }
            }
        };
        #endregion

        #region Filter/Order/Map
        public override IQueryable<Contributor> Query()
        {
            var query = db.Contributors.AsQueryable();

            return Filter(query);
        }

        public override IOrderedQueryable<Contributor> OrderQuery(IQueryable<Contributor> query)
        {
            this.orderedQueryBuilder.OrderFor(x => x.Name, y => y.FirstName + " " + y.LastName);

            var orderedQuery = this.orderedQueryBuilder.Order(query, x => x.OrderBy(y => y.StartDate));

            return orderedQuery;
        }

        public override IEnumerable<ContributorRowModel> MapQuery(IQueryable<Contributor> query)
        {
            return query.Select(MapContributor_ContributorRowModel);
        }

        public override void FillDetails(ContributorRowModel row)
        {
            row.Details = db.Contributors.Where(x => x.Id == row.Id).Select(MapContributor_ContributorDetailsModel).FirstOrDefault();
        }

        public IQueryable<Contributor> Filter(IQueryable<Contributor> query)
        {
            if (this.Settings != null)
            {
                if (!string.IsNullOrEmpty(Settings.QuickSearch))
                {
                    //join query with correspondant enum strings
                    var roleTypes = Enum.GetValues(typeof(ProjectRole));
                    var displayedRoles = new List<KeyValuePair<int, string>>();
                    foreach (Enum roleType in roleTypes)
                    {
                        var displayName = ReflectionHelpers.EnumDisplayName(typeof(ProjectRole), roleType);
                        displayedRoles.Add(new KeyValuePair<int, string>((int)(ProjectRole)roleType, displayName.ToLower()));
                    }

                    var searched = Settings.QuickSearch.ToLower();

                    //filter by fields that are strings on enums
                    var queryQuick = query.Join(displayedRoles, x => (int)x.Role, y => y.Key, (x, y) => new { Contributer = x, RoleText = y });
                    queryQuick = queryQuick.Where(x => x.Contributer.FirstName.ToLower().Contains(searched) ||
                                                        x.Contributer.LastName.ToLower().Contains(searched) ||
                                                        x.Contributer.Country.ToLower().Contains(searched) ||
                                                        x.RoleText.Value.Contains(searched) ||
                                                        x.Contributer.Languages != null && x.Contributer.Languages.Any(y => y.Contains(searched)) ||
                                                        x.Contributer.Contributions.Contains(searched));

                    //select back TEntity
                    query = queryQuick.Select(x => x.Contributer);
                }
                if (Settings.Search != null)
                {
                    #region Name
                    if (!string.IsNullOrEmpty(Settings.Search.Name))
                    {
                        var name = Settings.Search.Name.ToLower();
                        query = query.Where(x => x.FirstName.ToLower().Contains(name) ||
                                                 x.LastName.ToLower().Contains(name));
                    }
                    #endregion

                    #region Enabled
                    if (Settings.Search.IsEnabled != null && Settings.Search.IsEnabled.SelectedValues.HasValue)
                    {
                        var isEnabled = Settings.Search.IsEnabled.SelectedValues.Value;

                        if (isEnabled == YesNoValueTypes.Yes)
                        {
                            query = query.Where(x => x.Enabled);
                        }
                        else if (isEnabled == YesNoValueTypes.No)
                        {
                            query = query.Where(x => !x.Enabled);
                        }
                    }
                    #endregion

                    #region Contributor since range
                    if (Settings.Search.StartDateRange != null)
                    {
                        var fromDate = Settings.Search.StartDateRange.From;
                        var toDate = Settings.Search.StartDateRange.To;

                        if (fromDate.ItemValue.HasValue)
                        {
                            query = query.Where(x => x.StartDate >= fromDate.ItemValue.Value);
                        }

                        if (toDate.ItemValue.HasValue)
                        {
                            query = query.Where(x => x.StartDate <= toDate.ItemValue.Value);
                        }
                    }
                    #endregion

                    #region Location
                    if (Settings.Search.CountriesList != null)
                    {
                        var country = Settings.Search.CountriesList.SelectedValues;

                        if (!string.IsNullOrEmpty(country))
                        {
                            query = query.Where(x => x.Country == country);
                        }
                    }
                    #endregion

                    #region Role
                    if (Settings.Search.RoleList != null)
                    {
                        var role = Settings.Search.RoleList.SelectedValues;

                        if (role.HasValue)
                        {
                            query = query.Where(x => x.Role == role);
                        }
                    }

                    if (Settings.Search.RolesFilter != null && Settings.Search.RolesFilter.Any())
                    {
                        query = query.Where(x => Settings.Search.RolesFilter.Contains(x.Role));
                    }
                    #endregion

                    #region Programming Languages
                    if (Settings.Search.LanguagesList != null)
                    {
                        var languages = Settings.Search.LanguagesList.SelectedValues;

                        if (languages != null)
                        {
                            query = query.Where(x => x.Languages != null && x.Languages.Any(y => languages.Contains(y)));
                        }
                    }
                    #endregion

                    #region Age

                    if (Settings.Search.AgeRange != null && !string.IsNullOrEmpty(Settings.Search.AgeRange.TextValue))
                        query =
                            query.Where(
                                x =>
                                    x.Age.HasValue && x.Age >= Settings.Search.AgeRange.From.ItemValue &&
                                    x.Age <= Settings.Search.AgeRange.To.ItemValue);

                    #endregion
                }
            }

            return query;
        }

        public List<ContributorRowModel> GetItems(BsGridRepositorySettings<ContributorSearchModel> settings, List<int> ids)
        {
            this.settings = settings;

            var result = new List<ContributorRowModel>();

            // create filtered query
            var basicQuery = this.Query();

            if (ids != null && ids.Any())
            {
                basicQuery = basicQuery.Where(x => ids.Contains(x.Id));
            }
            else
            {
                basicQuery = Filter(basicQuery);
            }

            IEnumerable<ContributorRowModel> finalQuery = null;

            // order
            var orderedExcelQueryBuilder = new OrderedQueryBuilder<ContributorRowModel>(this.settings.OrderableColumns);

            orderedExcelQueryBuilder.OrderFor(x => x.Name, y => y.FirstName + " " + y.LastName);

            var orderedQuery = orderedExcelQueryBuilder.Order(basicQuery, x => x.OrderBy(y => y.StartDate));

            // map
            finalQuery = orderedQuery.Select(MapContributor_ContributorRowModel).ToList();

            result = finalQuery.ToList();

            return result;
        }
        #endregion

        #region CRUD
        public ContributorRowModel Create(ContributorNewModel model)
        {
            var entity = new Contributor
            {
                Id = db.Contributors.Count() + 1,
                Enabled = model.IsEnabled == YesNoValueTypes.Yes,
                FirstName = model.FirstName,
                LastName = model.LastName,
                Country = model.CountriesList.SelectedValues,
                Contributions = model.Contributions,
                Role = model.RoleList.SelectedValues.Value,
                Languages = model.LanguagesList.SelectedValues,
                StartDate = model.StartDate.DateValue.Value,
                Url = model.Url,
                Age = model.Age.ItemValue
            };
            db.Contributors.Add(entity);
            db.SaveChanges();

            return MapContributor_ContributorRowModel(entity);
        }

        public ContributorRowModel ReadRow(int objId)
        {
            return db.Contributors.Where(x => x.Id == objId).Select(MapContributor_ContributorRowModel).FirstOrDefault();
        }

        public List<ContributorRowModel> ReadRows(List<int> objIds)
        {
            return db.Contributors.Where(x => objIds.Contains(x.Id)).Select(MapContributor_ContributorRowModel).ToList();
        }

        public ContributorDetailsReadonly Update(ContributorDetailsEditable model, int objId, EditComponents componentId)
        {
            var entity = db.Contributors.FirstOrDefault(x => x.Id == objId);

            if (entity != null)
            {
                switch (componentId)
                {
                    case EditComponents.Identity:
                        entity.FirstName = model.Identity.FirstName;
                        entity.LastName = model.Identity.LastName;
                        entity.Url = model.Identity.Url;
                        entity.Country = model.Identity.CountriesList.SelectedValues;
                        entity.Age = model.Identity.Age.ItemValue;
                        break;
                    case EditComponents.ProjectRelated:
                        entity.Role = model.ProjectRelated.RoleList.SelectedValues.Value;
                        entity.StartDate = model.ProjectRelated.StartDate.DateValue.Value;
                        entity.Languages = model.ProjectRelated.LanguagesList.SelectedValues;
                        entity.Contributions = model.ProjectRelated.Contributions;
                        break;
                }
                db.SaveChanges();
            }

            return MapContributor_ContributorDetailsModel(entity);
        }

        public ContributorDetailsEditable ReadEditable(int objId, EditComponents componentId)
        {
            var model = db.Contributors.FirstOrDefault(x => x.Id == objId);
            var result = new ContributorDetailsEditable
            {
                Id = model.Id
            };

            if (componentId == EditComponents.Identity)
            {
                result.Identity = new ContributorIdentityEditableModel
                {
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    Url = model.Url,
                    CountriesList = Lists.AllCounties<string>(false),
                    Age = new BsRangeItem<int?>
                    {
                        ItemValue = model.Age,
                        MinValue = 14,
                        MaxValue = 100,
                        TextValue = model.Age.HasValue ? model.Age.Value.ToString() : string.Empty
                    }
                };
            }
            else if (componentId == EditComponents.ProjectRelated)
            {
                result.ProjectRelated = new ContributorProjectEditableRelatedModel
                {
                    Contributions = model.Contributions,
                    LanguagesList = Lists.AllLanguages<List<string>>(),
                    StartDate = new BsDateTime()
                    {
                        DateValue = model.StartDate
                    }
                };
            }

            if (result.Identity != null)
            {
                result.Identity.CountriesList.SelectedValues = model.Country;
            }

            if (result.ProjectRelated != null)
            {
                result.ProjectRelated.LanguagesList.SelectedValues = model.Languages;
                result.ProjectRelated.RoleList.SelectedValues = model.Role;
            }

            return result;
        }

        public void Reorder(List<ContributorOrderModel> model, int? parentId = null, bool saveChanges = true)
        {
            if (model != null)
            {
                var ids = model.Select(x => x.Id);
                var contributors = db.Contributors.Where(x => ids.Contains(x.Id));
                var order = 1;

                foreach (var item in model)
                {
                    var contributor = db.Contributors.FirstOrDefault(x => x.Id == item.Id);

                    if (contributor != null)
                    {
                        contributor.Order = order;
                        contributor.Id_Coordinator = parentId;

                        order++;

                        var subordinates = item.Subordinates;

                        if (subordinates != null && subordinates.Any())
                        {
                            Reorder(subordinates.ToList(), contributor.Id, false);
                        }
                    }
                }

                if (saveChanges)
                {
                    db.SaveChanges();
                }
            }

        }

        public void EnableDisable(int objId, bool? enable)
        {
            var entity = db.Contributors.FirstOrDefault(x => x.Id == objId);

            if (entity != null)
            {
                entity.Enabled = enable.HasValue ? enable.Value : !entity.Enabled;
                db.SaveChanges();
            }
        }

        public void Delete(int objId)
        {
            var entity = db.Contributors.FirstOrDefault(x => x.Id == objId);

            if (entity != null)
            {
                db.Contributors.Remove(entity);
                db.SaveChanges();
            }
        }
        #endregion

        #region Helpers
        [System.Diagnostics.DebuggerHidden()]
        public ContributorSearchModel GetSearchForm(ContributorSearchModel stateModel)
        {
            var model = new ContributorSearchModel();

            model.IsEnabled = new BsSelectList<YesNoValueTypes?>();
            model.IsEnabled.ItemsFromEnum(typeof(YesNoValueTypes));
            model.RoleList = new BsSelectList<ProjectRole?>();
            model.RoleList.ItemsFromEnum(typeof(ProjectRole));
            model.CountriesList = Lists.AllCounties<string>(false);
            model.LanguagesList = Lists.AllLanguages<List<string>>();

            if (stateModel != null)
            {
                if (stateModel.Name != null)
                {
                    model.Name = stateModel.Name;
                }

                if (stateModel.IsEnabled != null && stateModel.IsEnabled.SelectedValues.HasValue)
                {
                    model.IsEnabled.SelectedValues = stateModel.IsEnabled.SelectedValues;
                }
                else
                {
                    model.IsEnabled.SelectedValues = YesNoValueTypes.Both;
                }

                if (stateModel.RoleList != null && stateModel.RoleList.SelectedValues.HasValue)
                {
                    model.RoleList.SelectedValues = stateModel.RoleList.SelectedValues;
                }

                if (stateModel.CountriesList != null && stateModel.CountriesList.SelectedValues != null)
                {
                    model.CountriesList.SelectedValues = stateModel.CountriesList.SelectedValues;
                }

                if (stateModel.LanguagesList != null && stateModel.LanguagesList.SelectedValues != null)
                {
                    model.LanguagesList.SelectedValues = stateModel.LanguagesList.SelectedValues;
                }
            }

            if (model.StartDateRange == null)
            {
                model.StartDateRange = new BsRange<DateTime?>()
                {
                    From = new BsRangeItem<DateTime?>
                    {
                        ItemValue = new DateTime(2013, 8, 1)
                    },
                    To = new BsRangeItem<DateTime?>
                    {
                        ItemValue = DateTime.Now
                    }
                };
            }

            if (model.AgeRange == null)
            {
                model.AgeRange = new BsRange<int?>
                {
                    From = new BsRangeItem<int?>
                    {
                        ItemValue = 14,
                        MinValue = 10,
                        TextValue = "14",
                        Display = "Start"
                    },
                    To = new BsRangeItem<int?>
                    {
                        ItemValue = 32,
                        MaxValue = 100,
                        TextValue = "32",
                        Display = "End"
                    },
                    TextValue = "18 - 21"
                };
            }

            return model;
        }

        public ContributorNewModel GetNewForm()
        {
            return new ContributorNewModel()
            {
                CountriesList = Lists.AllCounties<string>(false),
                LanguagesList = Lists.AllLanguages<List<string>>(),
                Age = new BsRangeItem<int?>
                {
                    ItemValue = 21,
                    MaxValue = 100,
                    MinValue = 14
                }
            };
        }

        public List<ContributorOrderModel> GetOrderForm(bool searchInDepth = false, int depthLevel = 0)
        {
            if (!searchInDepth)
            {
                return db.Contributors.Where(x => x.Id_Coordinator == null).Select(x => new ContributorOrderModel
                {
                    Id = x.Id,
                    Order = x.Order,
                    Name = x.FirstName + " " + x.LastName,
                    Role = x.Role,
                    Depth = depthLevel,
                    ParentId = null,
                    Subordinates =
                        db.Contributors.Where(y => y.Id_Coordinator == x.Id).Select(y => new ContributorOrderModel
                        {
                            Id = y.Id,
                            Order = y.Order,
                            Name = y.FirstName + " " + y.LastName,
                            Role = y.Role,
                            Depth = depthLevel + 1,
                            ParentId = x.Id,
                            Subordinates = null

                        }).OrderBy(y => y.Order).ToList()

                }).OrderBy(x => x.Order).ToList();
            }

            return db.Contributors.Where(x => x.Id_Coordinator == null).Select(x => new ContributorOrderModel
            {
                Id = x.Id,
                Order = x.Order,
                Name = x.FirstName + " " + x.LastName,
                Role = x.Role,
                Depth = depthLevel,
                ParentId = null,
                Subordinates = GetSubordinatesFor(x.Id, depthLevel + 1)
            }).OrderBy(x => x.Order).ToList();


        }

        public List<ContributorOrderModel> GetSubordinatesFor(int coordinatorId, int depthLevel = 0)
        {
            var subordinates = new List<ContributorOrderModel>();

            var children = db.Contributors.Where(y => y.Id_Coordinator == coordinatorId)
                .Select(y => new ContributorOrderModel
                {
                    Id = y.Id,
                    Order = y.Order,
                    Name = y.FirstName + " " + y.LastName,
                    Role = y.Role,
                    Depth = depthLevel,
                    ParentId = coordinatorId

                }).OrderBy(y => y.Order);

            if (children != null && children.Any())
            {
                foreach (var child in children)
                {
                    subordinates.Add(new ContributorOrderModel
                    {
                        Id = child.Id,
                        Name = child.Name,
                        Order = child.Order,
                        Role = child.Role,
                        Depth = depthLevel,
                        ParentId = coordinatorId,
                        Subordinates = GetSubordinatesFor(child.Id, depthLevel + 1)
                    });
                }
            }

            return subordinates.Any() ? subordinates : null;
        }
        #endregion
    }
}
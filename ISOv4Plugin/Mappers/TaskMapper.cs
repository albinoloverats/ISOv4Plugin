﻿/*
 * ISO standards can be purchased through the ANSI webstore at https://webstore.ansi.org
*/

using AgGateway.ADAPT.ApplicationDataModel.Common;
using AgGateway.ADAPT.ApplicationDataModel.Documents;
using AgGateway.ADAPT.ApplicationDataModel.Guidance;
using AgGateway.ADAPT.ApplicationDataModel.LoggedData;
using AgGateway.ADAPT.ApplicationDataModel.Logistics;
using AgGateway.ADAPT.ApplicationDataModel.Prescriptions;
using AgGateway.ADAPT.ApplicationDataModel.Representations;
using AgGateway.ADAPT.ISOv4Plugin.ExtensionMethods;
using AgGateway.ADAPT.ISOv4Plugin.ISOModels;
using AgGateway.ADAPT.ISOv4Plugin.Representation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AgGateway.ADAPT.ISOv4Plugin.Mappers
{
    public interface ITaskMapper
    {
        IEnumerable<ISOTask> Export(IEnumerable<WorkItem> adaptWorkItems, int isoGridType);
        IEnumerable<ISOTask> Export(IEnumerable<LoggedData> adaptLoggedDatas);
        IEnumerable<WorkItem> ImportWorkItems(IEnumerable<ISOTask> isoFarms);
        IEnumerable<LoggedData> ImportLoggedDatas(IEnumerable<ISOTask> isoFarms);
    }

    public class TaskMapper : BaseMapper, ITaskMapper
    {
        CodedCommentListMapper _commentListMapper;
        CodedCommentMapper _commentMapper;
        GridMapper _gridMapper;
        public TaskMapper(TaskDataMapper taskDataMapper, CodedCommentListMapper commentListMapper, CodedCommentMapper commentMapper) : base(taskDataMapper, "TSK")
        {
            _commentListMapper = commentListMapper;
            _commentMapper = commentMapper;
            _gridMapper = new GridMapper(taskDataMapper);
        }

        #region Export

        #region Export Work Items
        public IEnumerable<ISOTask> Export(IEnumerable<WorkItem> adaptWorkItems, int isoGridType)
        {
            List<ISOTask> tasks = new List<ISOTask>();
            foreach (WorkItem workItem in adaptWorkItems)
            {
                IEnumerable<ISOTask> newTasks = Export(workItem, isoGridType);
                tasks.AddRange(newTasks);
            }
            return tasks;
        }

        private IEnumerable<ISOTask> Export(WorkItem workItem, int isoGridType)
        {
            List<ISOTask> tasks = new List<ISOTask>();

            if (workItem.WorkItemOperationIds.Any())
            {
                PrescriptionMapper rxMapper = new PrescriptionMapper(TaskDataMapper, _gridMapper, _commentMapper);
                foreach (int operationID in workItem.WorkItemOperationIds)
                {
                    WorkItemOperation operation = DataModel.Documents.WorkItemOperations.FirstOrDefault(o => o.Id.ReferenceId == operationID);
                    if (operation != null && operation.PrescriptionId.HasValue)
                    {
                        Prescription prescription = DataModel.Catalog.Prescriptions.FirstOrDefault(p => p.Id.ReferenceId == operation.PrescriptionId.Value);
                        if (prescription != null)
                        {
                            ISOTask task = rxMapper.ExportPrescription(workItem, isoGridType, prescription);
                            tasks.Add(task);
                        }
                    }
                }
            }

            return tasks;
        }

        #endregion Export Work Items

        #region Export Logged Data

        public IEnumerable<ISOTask> Export(IEnumerable<LoggedData> adaptLoggedDatas)
        {
            List<ISOTask> tasks = new List<ISOTask>();
            foreach (LoggedData loggedData in adaptLoggedDatas)
            {
                ISOTask task = Export(loggedData);
                tasks.Add(task);
            }
            return tasks;
        }

        private ISOTask Export(LoggedData loggedData)
        {
            ISOTask task = new ISOTask();

            //Task ID
            string taskID = loggedData.Id.FindIsoId() ?? GenerateId();
            task.TaskID = taskID;
            ExportUniqueIDs(loggedData.Id, taskID);
            TaskDataMapper.ISOIdMap.Add(loggedData.Id.ReferenceId, taskID);

            //Task Designator
            task.TaskDesignator = $"Logged Data {loggedData.Id.ReferenceId}";

            //Customer Ref
            if (loggedData.GrowerId.HasValue)
            {
                task.CustomerIdRef = TaskDataMapper.ISOIdMap.FindByADAPTId(loggedData.GrowerId.Value);
            }

            //Farm Ref
            if (loggedData.FarmId.HasValue)
            {
                task.FarmIdRef = TaskDataMapper.ISOIdMap.FindByADAPTId(loggedData.FarmId.Value);
            }

            //Partfield Ref
            if (loggedData.CropZoneId.HasValue)
            {
                task.PartFieldIdRef = TaskDataMapper.ISOIdMap.FindByADAPTId(loggedData.CropZoneId.Value);
            }
            else if (loggedData.FieldId.HasValue)
            {
                task.PartFieldIdRef = TaskDataMapper.ISOIdMap.FindByADAPTId(loggedData.FieldId.Value);
            }

            //Status
            task.TaskStatus = ISOEnumerations.ISOTaskStatus.Completed;

            //Time Logs
            if (loggedData.OperationData.Any())
            {
                TimeLogMapper tlogMapper = new TimeLogMapper(TaskDataMapper);
                task.TimeLogs = tlogMapper.ExportTimeLogs(loggedData.OperationData, TaskDataPath).ToList();
            }

            //Summaries
            if (loggedData.SummaryId.HasValue)
            {
                Summary summary = DataModel.Documents.Summaries.FirstOrDefault(s => s.Id.ReferenceId == loggedData.SummaryId.Value);
                if (summary != null)
                {
                    task.Times.AddRange(ExportSummary(summary));
                }
            }

            //Comments
            if (loggedData.Notes.Any())
            {
                CommentAllocationMapper canMapper = new CommentAllocationMapper(TaskDataMapper, _commentMapper);
                task.CommentAllocations = canMapper.ExportCommentAllocations(loggedData.Notes).ToList();
            }

            //Worker Allocations
            if (loggedData.PersonRoleIds.Any())
            {
                WorkerAllocationMapper workerAllocationMapper = new WorkerAllocationMapper(TaskDataMapper);
                List<PersonRole> personRoles = new List<PersonRole>();
                foreach (int id in loggedData.PersonRoleIds)
                {
                    PersonRole personRole = DataModel.Catalog.PersonRoles.FirstOrDefault(p => p.Id.ReferenceId == id);
                    if (personRole != null)
                    {
                        personRoles.Add(personRole);
                    }
                }
                task.WorkerAllocations = workerAllocationMapper.ExportWorkerAllocations(personRoles).ToList();
            }

            //Guidance Allocations
            if (loggedData.GuidanceAllocationIds.Any())
            {
                GuidanceAllocationMapper guidanceAllocationMapper = new GuidanceAllocationMapper(TaskDataMapper);
                List<GuidanceAllocation> allocations = new List<GuidanceAllocation>();
                foreach (int id in loggedData.GuidanceAllocationIds)
                {
                    GuidanceAllocation allocation = DataModel.Documents.GuidanceAllocations.FirstOrDefault(p => p.Id.ReferenceId == id);
                    if (allocation != null)
                    {
                        allocations.Add(allocation);
                    }
                }
                task.GuidanceAllocations = guidanceAllocationMapper.ExportGuidanceAllocations(allocations).ToList();
            }

            return task;
        }

        private List<ISOTime> ExportSummary(Summary summary)
        {
            List<ISOTime> times = new List<ISOTime>();
            foreach (StampedMeteredValues values in summary.SummaryData)
            {
                ISOTime time = new ISOTime();
                time.Start = values.Stamp.TimeStamp1;
                time.Stop = values.Stamp.TimeStamp2;
                if (values.Stamp.Duration.HasValue)
                {
                    time.Duration = (long)values.Stamp.Duration.Value.TotalSeconds;
                }
                time.Type = values.Stamp.DateContext == DateContextEnum.ProposedStart ? ISOEnumerations.ISOTimeType.Planned : ISOEnumerations.ISOTimeType.Effective;

                foreach (MeteredValue value in values.Values)
                {
                    if (value.Value != null && value.Value is NumericRepresentationValue)
                    {
                        NumericRepresentationValue numericValue = value.Value as NumericRepresentationValue;
                        ISODataLogValue dlv = new ISODataLogValue();
                        int? ddi = RepresentationMapper.Map(numericValue.Representation);
                        if (ddi.HasValue)
                        {
                            dlv.ProcessDataDDI = ddi.Value.AsHexDDI();
                            DdiDefinition DdiDefinition = DDIs[ddi.Value];
                            dlv.ProcessDataValue = (long)(numericValue.Value.Value / (DdiDefinition.Resolution != 0 ? DdiDefinition.Resolution : 1d));
                        }
                        else
                        {
                            if (numericValue.Representation.Code.Length == 4)
                            {
                                dlv.ProcessDataDDI = numericValue.Representation.Code;
                                dlv.ProcessDataValue = (long)numericValue.Value.Value;
                            }
                            
                        }
                        time.DataLogValues.Add(dlv);
                    }
                }
                times.Add(time);
            }
            return times;
        }

        #endregion Export LoggedData

        #endregion Export

        #region Import

        #region Import WorkItems

        public IEnumerable<WorkItem> ImportWorkItems(IEnumerable<ISOTask> isoPrescribedTasks)
        {
            List<WorkItem> adaptWorkItems = new List<WorkItem>();
            foreach (ISOTask isoPrescribedTask in isoPrescribedTasks)
            {
                WorkItem workItem = ImportWorkItem(isoPrescribedTask);
                adaptWorkItems.Add(workItem);
            }
            return adaptWorkItems;
        }

        private WorkItem ImportWorkItem(ISOTask isoPrescribedTask)
        {
            WorkItem workItem = new WorkItem();

            //Task ID
            workItem.Id.UniqueIds.AddRange(ImportUniqueIDs(isoPrescribedTask.TaskID));
            TaskDataMapper.ADAPTIdMap.Add(isoPrescribedTask.TaskID, workItem.Id.ReferenceId);

            //Grower ID
            workItem.GrowerId = TaskDataMapper.ADAPTIdMap.FindByISOId(isoPrescribedTask.CustomerIdRef);

            //Farm ID
            workItem.FarmId = TaskDataMapper.ADAPTIdMap.FindByISOId(isoPrescribedTask.FarmIdRef);

            //Field/CropZone
            int? pfdID = TaskDataMapper.ADAPTIdMap.FindByISOId(isoPrescribedTask.PartFieldIdRef);
            if (pfdID.HasValue)
            {
                if (DataModel.Catalog.CropZones.Any(c => c.Id.ReferenceId == pfdID.Value))
                {
                    workItem.CropZoneId = pfdID.Value;
                }
                else
                {
                    workItem.FieldId = pfdID.Value;
                }
            }

            //Status
            workItem.StatusUpdates = new List<StatusUpdate>() { new StatusUpdate() { Status = ImportStatus(isoPrescribedTask.TaskStatus) } };

            //Responsible Worker
            if (!string.IsNullOrEmpty(isoPrescribedTask.ResponsibleWorkerIdRef))
            {
                ISOWorker worker = ISOTaskData.ChildElements.OfType<ISOWorker>().FirstOrDefault(w => w.WorkerId == isoPrescribedTask.ResponsibleWorkerIdRef);
                if (TaskDataMapper.ADAPTIdMap.ContainsKey(isoPrescribedTask.ResponsibleWorkerIdRef))
                {
                    //Create a Role
                    int personID = TaskDataMapper.ADAPTIdMap[isoPrescribedTask.ResponsibleWorkerIdRef].Value;
                    PersonRole role = new PersonRole() { PersonId = personID };

                    //Add to Catalog
                    DataModel.Catalog.PersonRoles.Add(role);
                    if (workItem.PeopleRoleIds == null)
                    {
                        workItem.PeopleRoleIds = new List<int>();
                    }

                    //Add to Task
                    workItem.PeopleRoleIds.Add(role.Id.ReferenceId);
                }
            }

            //Worker Allocation
            if (isoPrescribedTask.WorkerAllocations.Any())
            {               
                WorkerAllocationMapper wanMapper = new WorkerAllocationMapper(TaskDataMapper);
                List<PersonRole> personRoles = wanMapper.ImportWorkerAllocations(isoPrescribedTask.WorkerAllocations).ToList();

                //Add to Catalog
                DataModel.Catalog.PersonRoles.AddRange(personRoles);

                //Add to Task
                if (workItem.PeopleRoleIds == null)
                {
                    workItem.PeopleRoleIds = new List<int>();
                }
                workItem.PeopleRoleIds.AddRange(personRoles.Select(p => p.Id.ReferenceId));
            }

            if (isoPrescribedTask.GuidanceAllocations.Any())
            {
                GuidanceAllocationMapper ganMapper = new GuidanceAllocationMapper(TaskDataMapper);
                List<GuidanceAllocation> allocations = ganMapper.ImportGuidanceAllocations(isoPrescribedTask.GuidanceAllocations).ToList();

                //Add to Catalog
                List<GuidanceAllocation> guidanceAllocations = DataModel.Documents.GuidanceAllocations as List<GuidanceAllocation>;
                if (guidanceAllocations != null)
                {
                    guidanceAllocations.AddRange(allocations);
                }

                //Add to Task
                if (workItem.GuidanceAllocationIds == null)
                {
                    workItem.GuidanceAllocationIds = new List<int>();
                }
                workItem.GuidanceAllocationIds.AddRange(allocations.Select(p => p.Id.ReferenceId));
            }

            //Prescription
            if (isoPrescribedTask.HasPrescription)
            {
                PrescriptionMapper rxMapper = new PrescriptionMapper(TaskDataMapper, _gridMapper, _commentMapper);
                Prescription rx = rxMapper.ImportPrescription(isoPrescribedTask, workItem);

                //Add to the Prescription the Catalog
                List<Prescription> prescriptions = DataModel.Catalog.Prescriptions as List<Prescription>;
                if (prescriptions != null)
                {
                    prescriptions.Add(rx);
                }

                //Add A WorkItemOperation
                WorkItemOperation operation = new WorkItemOperation();
                operation.PrescriptionId = rx.Id.ReferenceId;

                //Add the operation to the Documents and reference on the WorkItem
                List<WorkItemOperation> operations = DataModel.Documents.WorkItemOperations as List<WorkItemOperation>;
                if (operations != null)
                {
                    operations.Add(operation);
                }
                workItem.WorkItemOperationIds.Add(operation.Id.ReferenceId);
            }

            return workItem;
        }

        private WorkStatusEnum ImportStatus(ISOEnumerations.ISOTaskStatus isoStatus)
        {
            switch (isoStatus)
            {
                case ISOEnumerations.ISOTaskStatus.Canceled:
                    return WorkStatusEnum.Cancelled;
                case ISOEnumerations.ISOTaskStatus.Completed:
                    return WorkStatusEnum.Completed;
                case ISOEnumerations.ISOTaskStatus.Running:
                    return WorkStatusEnum.InProgress;
                case ISOEnumerations.ISOTaskStatus.Paused:
                    return WorkStatusEnum.Paused;
                case ISOEnumerations.ISOTaskStatus.Planned:
                case ISOEnumerations.ISOTaskStatus.Template:
                default:
                    return WorkStatusEnum.Scheduled;
            }
        }

        #endregion Import WorkItems

        #region Import LoggedData

        public IEnumerable<LoggedData> ImportLoggedDatas(IEnumerable<ISOTask> isoLoggedTasks)
        {
            List<LoggedData> adaptLoggedDatas = new List<LoggedData>();
            foreach (ISOTask isoLoggedTask in isoLoggedTasks)
            {
                LoggedData loggedData = ImportLoggedData(isoLoggedTask);
                adaptLoggedDatas.Add(loggedData);
            }
            return adaptLoggedDatas;
        }

        private LoggedData ImportLoggedData(ISOTask isoLoggedTask)
        {
            LoggedData loggedData = new LoggedData();
            loggedData.OperationData = new List<OperationData>();

            //Task ID
            loggedData.Id.UniqueIds.AddRange(ImportUniqueIDs(isoLoggedTask.TaskID));
            TaskDataMapper.ADAPTIdMap.Add(isoLoggedTask.TaskID, loggedData.Id.ReferenceId);

            //Task Name
            loggedData.Description = isoLoggedTask.TaskDesignator;

            //Grower ID
            loggedData.GrowerId = TaskDataMapper.ADAPTIdMap.FindByISOId(isoLoggedTask.CustomerIdRef);

            //Farm ID
            loggedData.FarmId = TaskDataMapper.ADAPTIdMap.FindByISOId(isoLoggedTask.FarmIdRef);

            //Field ID
            int? pfdID = TaskDataMapper.ADAPTIdMap.FindByISOId(isoLoggedTask.PartFieldIdRef);
            if (pfdID.HasValue)
            {
                if (DataModel.Catalog.CropZones.Any(c => c.Id.ReferenceId == pfdID.Value))
                {
                    loggedData.CropZoneId = pfdID.Value;
                }
                else
                {
                    loggedData.FieldId = pfdID.Value;
                }
            }

            //Responsible Worker
            if (!string.IsNullOrEmpty(isoLoggedTask.ResponsibleWorkerIdRef))
            {
                ISOWorker worker = ISOTaskData.ChildElements.OfType<ISOWorker>().FirstOrDefault(w => w.WorkerId == isoLoggedTask.ResponsibleWorkerIdRef);
                if (TaskDataMapper.ADAPTIdMap.ContainsKey(isoLoggedTask.ResponsibleWorkerIdRef))
                {
                    //Create a Role
                    int personID = TaskDataMapper.ADAPTIdMap[isoLoggedTask.ResponsibleWorkerIdRef].Value;
                    PersonRole role = new PersonRole() { PersonId = personID };

                    //Add to Catalog
                    DataModel.Catalog.PersonRoles.Add(role);
                    if (loggedData.PersonRoleIds == null)
                    {
                        loggedData.PersonRoleIds = new List<int>();
                    }

                    //Add to Task
                    loggedData.PersonRoleIds.Add(role.Id.ReferenceId);
                }
            }

            //Worker Allocations
            if (isoLoggedTask.WorkerAllocations.Any())
            {
                WorkerAllocationMapper wanMapper = new WorkerAllocationMapper(TaskDataMapper);
                List<PersonRole> personRoles = wanMapper.ImportWorkerAllocations(isoLoggedTask.WorkerAllocations).ToList();

                //Add to Catalog
                DataModel.Catalog.PersonRoles.AddRange(personRoles);
                if (loggedData.PersonRoleIds == null)
                {
                    loggedData.PersonRoleIds = new List<int>();
                }

                //Add to Task
                loggedData.PersonRoleIds.AddRange(personRoles.Select(p => p.Id.ReferenceId));
            }

            //Guidance Allocations
            if (isoLoggedTask.GuidanceAllocations.Any())
            {
                GuidanceAllocationMapper ganMapper = new GuidanceAllocationMapper(TaskDataMapper);
                List<GuidanceAllocation> allocations = ganMapper.ImportGuidanceAllocations(isoLoggedTask.GuidanceAllocations).ToList();

                //Add to Catalog
                List<GuidanceAllocation> guidanceAllocations = DataModel.Documents.GuidanceAllocations as List<GuidanceAllocation>;
                if (guidanceAllocations != null)
                {
                    guidanceAllocations.AddRange(allocations);
                }

                //Add to Task
                if (loggedData.GuidanceAllocationIds == null)
                {
                    loggedData.GuidanceAllocationIds = new List<int>();
                }
                loggedData.GuidanceAllocationIds.AddRange(allocations.Select(p => p.Id.ReferenceId));
            }

            //Comments
            if (isoLoggedTask.CommentAllocations.Any())
            {
                CommentAllocationMapper canMapper = new CommentAllocationMapper(TaskDataMapper, _commentMapper);
                loggedData.Notes = canMapper.ImportCommentAllocations(isoLoggedTask.CommentAllocations).ToList();
            }

            //Summaries
            if (isoLoggedTask.Times.Any(t => t.HasStart && t.HasStop && t.HasType)) //Nothing added without a Start, Stop, Type attributes
            {
                Summary summary = new Summary();
                summary.SummaryData = ImportSummaryData(isoLoggedTask);
                if (DataModel.Documents.Summaries == null)
                {
                    DataModel.Documents.Summaries = new List<Summary>();
                }
                (DataModel.Documents.Summaries as List<Summary>).Add(summary);
                loggedData.SummaryId = summary.Id.ReferenceId;
            }

            //Operation Data
            if (isoLoggedTask.TimeLogs.Any())
            {
                TimeLogMapper tlogMapper = new TimeLogMapper(TaskDataMapper);
                loggedData.OperationData = tlogMapper.ImportTimeLogs(isoLoggedTask.TimeLogs);
            }

            return loggedData;
        }

        private List<StampedMeteredValues> ImportSummaryData(ISOTask isoLoggedTask)
        {
            List<StampedMeteredValues> stampedValuesList = new List<StampedMeteredValues>();
            foreach (ISOTime time in isoLoggedTask.Times.Where(t => t.HasStart && t.HasStop && t.HasType)) //Nothing added without a Start, Stop, Type attributes
            {
                StampedMeteredValues stampedValues = new StampedMeteredValues();

                //TimeScope
                stampedValues.Stamp = new TimeScope();
                stampedValues.Stamp.TimeStamp1 = time.Start;
                stampedValues.Stamp.TimeStamp2 = time.Stop;
                if (time.Stop == null && time.Duration != null)
                {
                    //Calculate the Stop time if missing and duration present
                    stampedValues.Stamp.TimeStamp2 = stampedValues.Stamp.TimeStamp1.Value.AddSeconds(time.Duration.Value);
                }
                stampedValues.Stamp.DateContext = time.Type == ISOEnumerations.ISOTimeType.Planned ? DateContextEnum.ProposedStart : DateContextEnum.ActualStart;
                stampedValues.Stamp.Duration = stampedValues.Stamp.TimeStamp2.GetValueOrDefault() - stampedValues.Stamp.TimeStamp1.GetValueOrDefault();

                //Values
                foreach (ISODataLogValue dlv in time.DataLogValues)
                {
                    if (dlv.ProcessDataDDI == null)
                    {
                        return null;
                    }
                    int ddi = dlv.ProcessDataDDI.AsInt32DDI();

                    long dataValue = 0;
                    if (dlv.ProcessDataValue.HasValue)
                    {
                        dataValue = dlv.ProcessDataValue.Value;
                    }

                    var unitOfMeasure = RepresentationMapper.GetUnitForDdi(ddi);
                    if (!DDIs.ContainsKey(ddi) || unitOfMeasure == null)
                    {
                        return null;
                    }

                    DdiDefinition ddiDefintion = DDIs[ddi];
                    stampedValues.Values.Add(new MeteredValue
                    {
                        Value = new NumericRepresentationValue(RepresentationMapper.Map(ddi) as NumericRepresentation,
                            unitOfMeasure, new NumericValue(unitOfMeasure, dataValue * ddiDefintion.Resolution))
                    });
                }
                stampedValuesList.Add(stampedValues);
            }
            return stampedValuesList;
        }

        #endregion Import Logged Data

        #endregion Import

    }
}
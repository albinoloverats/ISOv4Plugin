﻿using System.Collections.Generic;
using System.Linq;
using AgGateway.ADAPT.ApplicationDataModel.ADM;
using AgGateway.ADAPT.ApplicationDataModel.LoggedData;
using AgGateway.ADAPT.ISOv4Plugin.Extensions;
using AgGateway.ADAPT.ISOv4Plugin.Models;

namespace AgGateway.ADAPT.ISOv4Plugin.ExportMappers
{
    public interface ITaskMapper
    {
        IEnumerable<TSK> Map(IEnumerable<LoggedData> loggedData, Catalog catalog, string datacardPath, int numberOfExistingTasks);
    }

    public class TaskMapper : ITaskMapper
    {
        private readonly ITimeMapper _timeMapper;
        private readonly ITlgMapper _tlgMapper;

        public TaskMapper() : this(new TimeMapper(), new TlgMapper())
        {
            
        }

        public TaskMapper(ITimeMapper timeMapper, ITlgMapper tlgMapper)
        {
            _timeMapper = timeMapper;
            _tlgMapper = tlgMapper;
        }

        public IEnumerable<TSK> Map(IEnumerable<LoggedData> loggedData, Catalog catalog, string datacardPath, int numberOfExistingTasks)
        {
            if (loggedData == null)
                yield break;

            var loggedDataList = loggedData.ToList();
            for (int i = 0; i < loggedDataList.Count(); ++i)
            {
                yield return Map(loggedDataList[i], catalog, datacardPath, numberOfExistingTasks + (i + 1));
            }
        }

        private TSK Map(LoggedData loggedData, Catalog catalog, string datacardPath, int taskNumber)
        {
            var tsk = new TSK
            {
                A = "TSK" + taskNumber,
                C = FindGrowerId(loggedData.GrowerId, catalog),
                D = FindFarmId(loggedData.FarmId, catalog),
                E = FindFieldId(loggedData.FieldId, catalog),
                G = TSKG.Item4,
                Items = MapItems(loggedData, catalog, datacardPath)
            };

            return tsk;
        }

        private object[] MapItems(LoggedData loggedData, Catalog catalog, string datacardPath)
        {
            var times = FindAndMapTimes(loggedData.TimeScopeIds, catalog);
            var tlgs = _tlgMapper.Map(loggedData.OperationData, datacardPath);

            var items = new List<object>();
            
            if(times != null)
                items.AddRange(times);

            if(tlgs != null)
                items.AddRange(tlgs);

            return items.ToArray();
        }

        private IEnumerable<TIM> FindAndMapTimes(List<int> timeScopeIds, Catalog catalog)
        {
            if (timeScopeIds == null)
                return null;

            var timescopes = catalog.TimeScopes.Where(x => timeScopeIds.Contains(x.Id.ReferenceId));
            return _timeMapper.Map(timescopes);
        }

        private string FindFieldId(int? fieldId, Catalog catalog)
        {
            if (catalog.Fields == null)
                return null;

            var field = catalog.Fields.SingleOrDefault(x => x.Id.ReferenceId == fieldId);
            return field == null ? null : field.Id.FindIsoId();  
        }

        private string FindFarmId(int? farmId, Catalog catalog)
        {
            if(catalog.Farms == null)
                return null;

            var farm = catalog.Farms.SingleOrDefault(x => x.Id.ReferenceId == farmId);
            return farm == null ? null : farm.Id.FindIsoId();
        }

        private string FindGrowerId(int? growerId, Catalog catalog)
        {
            if (catalog.Growers == null)
                return null;

            var grower = catalog.Growers.SingleOrDefault(x => x.Id.ReferenceId == growerId);
            return grower == null ? null : grower.Id.FindIsoId();
        }
    }
}
﻿using System;
using FourRoads.TelligentCommunity.MigratorFramework.Entities;

namespace FourRoads.TelligentCommunity.MigratorFramework.Interfaces
{
    public interface IMigrationObjectHandler
    {
        IPagedList<string> ListObjectKeys(int pageSize, int pageIndex);

        MigratedObject MigrateObject(string key, IMigrationVisitor migrationVisitor, bool updateIfExistsInDestination, int cutoffDays);

        bool MigratedObjectExists(MigratedData data);

        string ObjectType { get; }

        /// <summary>
        /// Runs before a migration starts
        /// </summary>
        void PreMigration(IMigrationVisitor visitor);

        /// <summary>
        /// Runs after a migration ends
        /// </summary>
        void PostMigration(IMigrationVisitor visitor);
    }
}
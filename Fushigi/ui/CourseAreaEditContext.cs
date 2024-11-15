﻿using Fasterflect;
using Fushigi.course;
using Fushigi.Logger;
using Fushigi.ui.undo;
using Fushigi.util;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using static Fushigi.course.CourseActorToRailLinksHolder;

namespace Fushigi.ui
{
    class CourseAreaEditContext(CourseArea area) : EditContextBase
    {
        public void AddActor(CourseActor actor)
        {
            LogAdding<CourseActor>($"{actor.mPackName}[{actor.mHash}]");

            CommitAction(area.mActorHolder.mActors
                .RevertableAdd(actor, $"{IconUtil.ICON_PLUS_CIRCLE} Add {actor.mPackName}"));
        }

        public void DeleteActor(CourseActor actor)
        {
            LogDeleting<CourseActor>($"{actor.mPackName}[{actor.mHash}]");

            var batchAction = BeginBatchAction();
            Deselect(actor);
            RemoveActorFromAllGroups(actor);
            DeleteLinksWithSource(actor.mHash);
            DeleteLinksWithDest(actor.mHash);
            DeleteRailLinkWithSrcActor(actor.mHash);
            CommitAction(area.mActorHolder.mActors
                .RevertableRemove(actor));

            batchAction.Commit($"{IconUtil.ICON_TRASH} Delete {actor.mPackName}");
        }

        public void AddActorToGroup(CourseGroup group, CourseActor actor)
        {
            Logger.Logger.LogMessage("CourseAreaEditContext", $"Adding actor {actor.mPackName}[{actor.mHash}] to group [{group.mHash}].");
            CommitAction(
                group.mActors.RevertableAdd(actor.mHash,
                $"Add {actor.mPackName} to simultaneous group")
            );
        }

        private void RemoveActorFromAllGroups(CourseActor actor)
        {
            foreach (var group in area.mGroupsHolder.GetGroupsContaining(actor.mHash))
                RemoveActorFromGroup(group, actor);
        }

        public void RemoveActorFromGroup(CourseGroup group, CourseActor actor)
        {
            Logger.Logger.LogError("CourseAreaEditContext", $"Removing actor {actor.mPackName}[{actor.mHash}] from group [{group.mHash}].");
            if (group.TryGetIndexOfActor(actor.mHash, out int index))
            {
                CommitAction(
                        group.mActors.RevertableRemoveAt(index,
                        $"Remove {actor.mPackName} from simultaneous group")
                    );
            }
        }

        public void AddLink(CourseLink link)
        {
            var linkList = area.mLinkHolder.mLinks;
            LogAdding<CourseLink>($": {link.mSource} -{link.mLinkName}-> {link.mDest}");

            //Checks if the the source actor already has links
            if (linkList.Any(x => x.mSource == link.mSource)){

                //Looks through the source actor's links
                //Then looks through it's links of the same type (If it has any)
                //Placing the new link in the right spot
                var index = linkList.FindLastIndex(x => x.mSource == link.mSource &&
                        (!linkList.Any(y => x.mLinkName == link.mLinkName) ||
                        x.mLinkName == link.mLinkName));

                CommitAction(
                    area.mLinkHolder.mLinks.RevertableInsert(link, index+1,
                        $"{IconUtil.ICON_PLUS_CIRCLE} Add {link.mLinkName} Link")
                );
                return;
            }
            else{
                //If it's the actor's first link
                CommitAction(
                    area.mLinkHolder.mLinks.RevertableAdd(link,
                        $"{IconUtil.ICON_PLUS_CIRCLE} Add {link.mLinkName} Link")
                );
            }
        }

        private void DeleteLinksWithDest(ulong hash)
        {
            foreach (var index in area.mLinkHolder.GetIndicesOfLinksWithDest_ForDelete(hash))
                DeleteLinkByIndex(index);
        }

        private void DeleteLinksWithSource(ulong hash)
        {
            foreach (var index in area.mLinkHolder.GetIndicesOfLinksWithSrc_ForDelete(hash))
                DeleteLinkByIndex(index);
        }

        //I don't like this method but there is one instance where we need it
        public void DeleteLink(string name, ulong src, ulong dest)
        {
            int index = area.mLinkHolder.mLinks.FindIndex(
                x => x.mSource == src && 
                x.mLinkName == name && 
                x.mDest == dest);
            DeleteLinkByIndex(index);
        }

        public void DeleteLink(CourseLink link)
        {
            int index = area.mLinkHolder.mLinks.IndexOf(link);
            DeleteLinkByIndex(index);
        }

        private void DeleteLinkByIndex(int index)
        {
            var link = area.mLinkHolder.mLinks[index];
            LogDeleting<CourseLink>($": {link.mSource} -{link.mLinkName}-> {link.mDest}");
            CommitAction(
                area.mLinkHolder.mLinks.RevertableRemoveAt(index, 
                $"{IconUtil.ICON_TRASH} Delete {link.mLinkName} Link")
            );
        }

        public void AddRail(CourseRail rail)
        {
            LogAdding<CourseRail>(rail.mHash);
            CommitAction(area.mRailHolder.mRails.RevertableAdd(rail,
                $"{IconUtil.ICON_PLUS_CIRCLE} Add Rail"));
        }

        public void DeleteRail(CourseRail rail)
        {
            LogDeleting<CourseRail>(rail.mHash);

            var batchAction = BeginBatchAction();
            DeleteRailLinkWithDestRail(rail.mHash);
            CommitAction(area.mRailHolder.mRails.RevertableRemove(rail));

            batchAction.Commit($"{IconUtil.ICON_TRASH} Delete Rail");
        }

        public void AddRailLink(CourseActorToRailLink link)
        {
            LogAdding<CourseActorToRailLink>(
                $": {link.mSourceActor} -{link.mLinkName}-> {link.mDestRail}[{link.mDestPoint}]");
            CommitAction(area.mRailLinksHolder.mLinks.RevertableAdd(link,
                $"{IconUtil.ICON_PLUS_CIRCLE} Add Actor to Rail Link"));
        }

        private void DeleteRailLinkWithSrcActor(ulong hash)
        {
            if (area.mRailLinksHolder.TryGetLinkWithSrcActor(hash, out var link))
            {
                DeleteRailLink(link);
            }
        }

        private void DeleteRailLinkWithDestRail(ulong hash)
        {
            if (area.mRailLinksHolder.TryGetLinkWithDestRail(hash, out var link))
            {
                DeleteRailLink(link);
            }
        }

        public void DeleteRailLinkWithDestPoint(CourseRail rail, CourseRail.CourseRailPoint point)
        {
            if (area.mRailLinksHolder.TryGetLinkWithDestRailAndPoint(rail.mHash, point.mHash, out var link))
            {
                DeleteRailLink(link);
            }
        }

        public void DeleteRailLink(CourseActorToRailLink link)
        {
            LogDeleting<CourseActorToRailLink>(
                $": {link.mSourceActor} -{link.mLinkName}-> {link.mDestRail}[{link.mDestPoint}]");
            CommitAction(area.mRailLinksHolder.mLinks.RevertableRemove(link,
                $"{IconUtil.ICON_TRASH} Delete Actor to Rail Link"));
        }


        public void AddBgUnit(CourseUnit unit)
        {
            LogAdding<CourseUnit>();
            CommitAction(area.mUnitHolder.mUnits.RevertableAdd(unit,
                    $"{IconUtil.ICON_PLUS_CIRCLE} Add Tile Unit"));
        }

        public void DeleteBgUnit(CourseUnit unit)
        {
            LogDeleting<CourseUnit>();
            CommitAction(area.mUnitHolder.mUnits.RevertableRemove(unit,
                    $"{IconUtil.ICON_TRASH} Delete Tile Unit"));
        }

        public void AddWall(CourseUnit unit, Wall wall)
        {
            LogAdding<Wall>();
            CommitAction(unit.Walls.RevertableAdd(wall,
                    $"{IconUtil.ICON_PLUS_CIRCLE} Add Wall"));
        }

        public void DeleteWall(CourseUnit unit, Wall wall)
        {
            LogDeleting<Wall>();
            CommitAction(unit.Walls.RevertableRemove(wall,
                    $"{IconUtil.ICON_TRASH} Delete Wall"));
        }

        public void AddBeltRail(CourseUnit unit, BGUnitRail rail)
        {
            LogAdding<BGUnitRail>();
            CommitAction(unit.mBeltRails.RevertableAdd(rail,
                    $"{IconUtil.ICON_PLUS_CIRCLE} Add Belt"));
        }

        public void DeleteBeltRail(CourseUnit unit, BGUnitRail rail)
        {
            LogDeleting<BGUnitRail>();
            CommitAction(unit.mBeltRails.RevertableRemove(rail,
                    $"{IconUtil.ICON_TRASH} Delete Belt"));
        }

        private void LogAdding<T>(ulong hash) => 
            LogAdding<T>($"[{hash}]");

        private void LogAdding<T>(string? extraText = null)
        {
            string text = $"Adding {typeof(T).Name()}";
            if (extraText != null)
                text += $" {extraText}";
            Logger.Logger.LogMessage("CourseAreaEditContext", text);
        }

        private void LogDeleting<T>(ulong hash) => 
            LogDeleting<T>($"[{hash}]");

        private void LogDeleting<T>(string? extraText = null)
        {
            string text = $"Deleting {typeof(T).Name()}";
            if (extraText != null)
                text += $" {extraText}";
            Logger.Logger.LogMessage("CourseAreaEditContext", text);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;

namespace Orleans.Activities.Test.Activities
{
    public sealed class WaitForBookmarkNoPersist : NativeActivity
    {
        [RequiredArgument]
        public InArgument<string> BookmarkName { get; set; }

        private Variable<NoPersistHandle> noPersistHandle;
        private Variable<Bookmark> bookmark;

        public WaitForBookmarkNoPersist()
        {
            noPersistHandle = new Variable<NoPersistHandle>();
            bookmark = new Variable<Bookmark>();
        }

        protected override bool CanInduceIdle => true;

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationVariable(noPersistHandle);
            metadata.AddImplementationVariable(bookmark);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            string bookmarkName = BookmarkName.Get(context);
            NoPersistHandle noPersistHandleToEnter = noPersistHandle.Get(context);
            noPersistHandleToEnter.Enter(context);
            Bookmark bookmark = context.CreateBookmark(bookmarkName,
                (NativeActivityContext _callbackContext, Bookmark _bookmark, object _state) =>
                {
                    NoPersistHandle noPersistHandleToExit = noPersistHandle.Get(_callbackContext);
                    noPersistHandleToExit.Exit(_callbackContext); 
                });
            this.bookmark.Set(context, bookmark);
        }

        protected override void Cancel(NativeActivityContext context)
        {
            Bookmark bookmark = this.bookmark.Get(context);
            if (bookmark != null)
            {
                context.RemoveBookmark(bookmark);
            }
            base.Cancel(context);
        }
    }
}

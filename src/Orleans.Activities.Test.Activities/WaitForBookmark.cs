using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Activities;

namespace Orleans.Activities.Test.Activities
{
    public sealed class WaitForBookmark : NativeActivity
    {
        [RequiredArgument]
        public InArgument<string> BookmarkName { get; set; }

        private Variable<Bookmark> bookmark;

        public WaitForBookmark()
        {
            bookmark = new Variable<Bookmark>();
        }

        protected override bool CanInduceIdle => true;

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationVariable(bookmark);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            string bookmarkName = BookmarkName.Get(context);
            Bookmark bookmark = context.CreateBookmark(bookmarkName);
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

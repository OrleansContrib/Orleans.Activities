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

        private Variable<Bookmark> bookmark = new Variable<Bookmark>();

        protected override bool CanInduceIdle => true;

        protected override void CacheMetadata(NativeActivityMetadata metadata)
        {
            metadata.AddImplementationVariable(this.bookmark);
            base.CacheMetadata(metadata);
        }

        protected override void Execute(NativeActivityContext context)
        {
            var bookmarkName = this.BookmarkName.Get(context);
            var bookmark = context.CreateBookmark(bookmarkName);
            this.bookmark.Set(context, bookmark);
        }

        protected override void Cancel(NativeActivityContext context)
        {
            var bookmark = this.bookmark.Get(context);
            if (bookmark != null)
            {
                context.RemoveBookmark(bookmark);
            }
            base.Cancel(context);
        }
    }
}

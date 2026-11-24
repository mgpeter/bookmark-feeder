// The root query keys shared across src/api/*. They live here rather than in their own modules
// because mutations in one area invalidate the others (a bookmark edit changes tag counts, a tag
// rename changes bookmarks), and importing the modules from each other would be circular.
export const bookmarksKey = 'bookmarks'
export const tagsKey = 'tags'
export const categoriesKey = 'categories'

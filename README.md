⚙️ Configurable via JSON

Admins can edit WordFilter.json to add/remove filters.

Each filter consists of a Word and a Replacement.

Example:

{
  "ShouldLog": true,
  "WordFilter": [{ "Word": "fuck*", "Replacement": "fluffy" }]
}

💬 Server command (/wordfilter)

/wordfilter list → shows current filters.

/wordfilter add <word> <replacement> → add a new filter.

/wordfilter remove <word> → remove a filter.

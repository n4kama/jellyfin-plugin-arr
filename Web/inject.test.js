// Self-check for the pure helpers in inject.js. Run: node Web/inject.test.js
const assert = require('assert');
const { parseItemId, pickLabel, buildHref } = require('./inject.js');

// parseItemId pulls a 32-hex id out of the location hash regardless of param order / prefix.
assert.strictEqual(parseItemId('#/details?id=0123456789abcdef0123456789abcdef&serverId=x'), '0123456789abcdef0123456789abcdef');
assert.strictEqual(parseItemId('#!/details?serverId=x&id=ABCDEF0123456789ABCDEF0123456789'), 'abcdef0123456789abcdef0123456789');
assert.strictEqual(parseItemId('#/home.html'), null);
assert.strictEqual(parseItemId(''), null);
assert.strictEqual(parseItemId('#/details?id=notavalidid'), null);

// pickLabel routes movies to Radarr, everything in the show hierarchy to Sonarr, else nothing.
assert.strictEqual(pickLabel('Movie'), 'Radarr');
assert.strictEqual(pickLabel('Series'), 'Sonarr');
assert.strictEqual(pickLabel('Season'), 'Sonarr');
assert.strictEqual(pickLabel('Episode'), 'Sonarr');
assert.strictEqual(pickLabel('MusicAlbum'), null);

// buildHref trims a trailing slash and only appends an (encoded) api_key when present.
assert.strictEqual(buildHref('http://h/', 'ID', null), 'http://h/Arr/Resolve/ID');
assert.strictEqual(buildHref('http://h', 'ID', 'tok en'), 'http://h/Arr/Resolve/ID?api_key=tok%20en');

console.log('inject.js self-check passed');

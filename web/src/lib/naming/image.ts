import { NamingError } from './types';

/**
 * A page as the model sees it, for the servers that cannot read text. The browser already has
 * this image on screen, so fetching it costs nothing on the wire.
 */

/** Roughly what a provider will take in one request, and well above a 1600px webp page. */
const MAX_BYTES = 4_000_000;

export async function fetchPageImage(
	url: string,
	get: typeof fetch = fetch
): Promise<{ mediaType: string; base64: string }> {
	const response = await get(url);
	if (!response.ok) {
		throw new NamingError(`The page image could not be read back (${response.status}).`);
	}

	const blob = await response.blob();
	if (blob.size > MAX_BYTES) {
		throw new NamingError('That page is too large to send to a model.');
	}

	const bytes = new Uint8Array(await blob.arrayBuffer());
	let binary = '';
	// Chunked: one apply() over a megabyte of arguments blows the stack.
	for (let i = 0; i < bytes.length; i += 8192) {
		binary += String.fromCharCode(...bytes.subarray(i, i + 8192));
	}

	return { mediaType: blob.type || 'image/webp', base64: btoa(binary) };
}

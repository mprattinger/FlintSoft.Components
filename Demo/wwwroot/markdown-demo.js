const images = new Set();

export function createImageUrl(data, contentType) {
    const url = URL.createObjectURL(new Blob([data], { type: contentType }));
    images.add(url);
    return url;
}

export function revokeImages() {
    for (const url of images) URL.revokeObjectURL(url);
    images.clear();
}

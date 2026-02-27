import { useState, useCallback } from 'react';
import './CoverImage.css';

export interface CoverImageProps {
  src: string | null | undefined;
  alt: string;
  className?: string;
  width?: number | string;
  height?: number | string;
  placeholderIcon?: React.ReactNode;
  style?: React.CSSProperties;
}

const DEFAULT_PLACEHOLDER = 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="100" height="150" viewBox="0 0 100 150"%3E%3Crect fill="%232a2d35" width="100" height="150"/%3E%3Ctext fill="%236b7280" font-family="sans-serif" font-size="10" x="50" y="75" text-anchor="middle"%3ENo Cover%3C/text%3E%3C/svg%3E';

export function CoverImage({
  src,
  alt,
  className = '',
  width,
  height,
  placeholderIcon,
  style,
}: CoverImageProps) {
  const [isLoading, setIsLoading] = useState(true);
  const [hasError, setHasError] = useState(false);

  const handleLoad = useCallback(() => {
    setIsLoading(false);
  }, []);

  const handleError = useCallback((e: React.SyntheticEvent<HTMLImageElement>) => {
    setIsLoading(false);
    setHasError(true);
    (e.target as HTMLImageElement).src = DEFAULT_PLACEHOLDER;
  }, []);

  const effectiveSrc = src || DEFAULT_PLACEHOLDER;

  return (
    <div 
      className={`cover-image-wrapper ${className}`}
      style={{ width, height, ...style }}
    >
      {isLoading && (
        <div className="cover-image-skeleton">
          {placeholderIcon || (
            <div className="cover-image-skeleton-pulse" />
          )}
        </div>
      )}
      <img
        src={effectiveSrc}
        alt={alt}
        className={`cover-image ${isLoading ? 'cover-image-loading' : ''} ${hasError ? 'cover-image-error' : ''}`}
        loading="lazy"
        decoding="async"
        onLoad={handleLoad}
        onError={handleError}
      />
    </div>
  );
}

export default CoverImage;

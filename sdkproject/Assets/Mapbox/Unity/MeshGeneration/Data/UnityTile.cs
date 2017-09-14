namespace Mapbox.Unity.MeshGeneration.Data
{
	using UnityEngine;
	using Mapbox.Unity.MeshGeneration.Enums;
	using Mapbox.Unity.Utilities;
	using Utils;
	using Mapbox.Map;
	using System;
	using Mapbox.Unity.Map;
	using System.Collections.Generic;

	public class UnityTile : MonoBehaviour
	{
		[SerializeField]
		Texture2D _rasterData;

		float[] _heightData;

		float _relativeScale;

		Texture2D _heightTexture;

		List<Tile> _tiles = new List<Tile>();

		MeshRenderer _meshRenderer;
		public MeshRenderer MeshRenderer
		{
			get
			{
				if (_meshRenderer == null)
				{
					_meshRenderer = GetComponent<MeshRenderer>();
				}
				return _meshRenderer;
			}
		}

		private MeshFilter _meshFilter;
		public MeshFilter MeshFilter
		{
			get
			{
				if (_meshFilter == null)
				{
					_meshFilter = GetComponent<MeshFilter>();
				}
				return _meshFilter;
			}
		}

		private Collider _collider;
		public Collider Collider
		{
			get
			{
				if (_collider == null)
				{
					_collider = GetComponent<Collider>();
				}
				return _collider;
			}
		}

		// TODO: should this be a string???
		string _vectorData;
		public string VectorData
		{
			get { return _vectorData; }
			set
			{
				_vectorData = value;
				OnVectorDataChanged(this);
			}
		}
		RectD _rect;
		public RectD Rect
		{
			get
			{
				return _rect;
			}
		}

		UnwrappedTileId _unwrappedTileId;
		public UnwrappedTileId UnwrappedTileId
		{
			get
			{
				return _unwrappedTileId;

			}
		}

		CanonicalTileId _canonicalTileId;
		public CanonicalTileId CanonicalTileId
		{
			get
			{
				return _canonicalTileId;
			}
		}


		public Material LoadingIndicatorMaterial;
		public Material _cachedMaterial;

		public TilePropertyState RasterDataState;
		public TilePropertyState HeightDataState;
		public TilePropertyState VectorDataState;

		public event Action<UnityTile> OnHeightDataChanged = delegate { };
		public event Action<UnityTile> OnRasterDataChanged = delegate { };
		public event Action<UnityTile> OnVectorDataChanged = delegate { };

		internal void Initialize(IMap map, UnwrappedTileId tileId)
		{
			_relativeScale = 1 / Mathf.Cos(Mathf.Deg2Rad * (float)map.CenterLatitudeLongitude.x);
			_rect = Conversions.TileBounds(tileId);
			_unwrappedTileId = tileId;
			_canonicalTileId = tileId.Canonical;
			gameObject.name = _canonicalTileId.ToString();
			var position = new Vector3((float)(_rect.Center.x - map.CenterMercator.x), 0, (float)(_rect.Center.y - map.CenterMercator.y));
			transform.localPosition = position;
			_cachedMaterial = MeshRenderer.material;
			gameObject.SetActive(true);
		}

		internal void Recycle()
		{
			// TODO: to hide potential visual artifacts, use placeholder mesh / texture?
			//MeshRenderer.enabled = false;
			MeshRenderer.material = LoadingIndicatorMaterial;
			gameObject.SetActive(false);

			// Reset internal state.
			RasterDataState = TilePropertyState.None;
			HeightDataState = TilePropertyState.None;
			VectorDataState = TilePropertyState.None;

			OnHeightDataChanged = delegate { };
			OnRasterDataChanged = delegate { };
			OnVectorDataChanged = delegate { };

			Cancel();
			_tiles.Clear();

			// HACK: this is for vector layer features and such.
			// It's slow and wasteful, but a better solution will be difficult.
			var childCount = transform.childCount;
			if (childCount > 0)
			{
				for (int i = 0; i < childCount; i++)
				{
					Destroy(transform.GetChild(i).gameObject);
				}
			}
		}

		internal void SetHeightData(byte[] data, float heightMultiplier = 1f, bool useRelative = false)
		{
			// HACK: compute height values for terrain. We could probably do this without a texture2d.
			if (_heightTexture == null)
			{
				_heightTexture = new Texture2D(0, 0);
			}

			_heightTexture.LoadImage(data);
			byte[] rgbData = _heightTexture.GetRawTextureData();

			// Get rid of this temporary texture. We don't need to bloat memory.
			_heightTexture.LoadImage(null);

			if (_heightData == null)
			{
				_heightData = new float[256 * 256];
			}

			var relativeScale = useRelative ? _relativeScale : 1f;
			for (int xx = 0; xx < 256; ++xx)
			{
				for (int yy = 0; yy < 256; ++yy)
				{
					float r = rgbData[(xx * 256 + yy) * 4 + 1];
					float g = rgbData[(xx * 256 + yy) * 4 + 2];
					float b = rgbData[(xx * 256 + yy) * 4 + 3];
					_heightData[xx * 256 + yy] = relativeScale * heightMultiplier * Conversions.GetAbsoluteHeightFromColor(r, g, b);
				}
			}

			HeightDataState = TilePropertyState.Loaded;
			OnHeightDataChanged(this);
		}

		public float QueryHeightData(float x, float y)
		{
			if (_heightData != null)
			{
				var intX = (int)Mathf.Clamp(x * 256, 0, 255);
				var intY = (int)Mathf.Clamp(y * 256, 0, 255);
				return _heightData[intY * 256 + intX];
			}

			return 0;
		}

		public void SetRasterData(byte[] data, bool useMipMap, bool useCompression)
		{
			MeshRenderer.material = _cachedMaterial;
			// Don't leak the texture, just reuse it.
			if (_rasterData == null)
			{
				_rasterData = new Texture2D(0, 0, TextureFormat.RGB24, useMipMap);
				_rasterData.wrapMode = TextureWrapMode.Clamp;
				MeshRenderer.material.mainTexture = _rasterData;
			}

			_rasterData.LoadImage(data);
			if (useCompression)
			{
				// High quality = true seems to decrease image quality?
				_rasterData.Compress(false);
			}

			MeshRenderer.material.mainTexture = _rasterData;
			MeshRenderer.enabled = true;
			RasterDataState = TilePropertyState.Loaded;
			OnRasterDataChanged(this);
		}

		public Texture2D GetRasterData()
		{
			return _rasterData;
		}

		internal void AddTile(Tile tile)
		{
			_tiles.Add(tile);
		}

		public void Cancel()
		{
			for (int i = 0, _tilesCount = _tiles.Count; i < _tilesCount; i++)
			{
				_tiles[i].Cancel();
			}
		}
	}
}
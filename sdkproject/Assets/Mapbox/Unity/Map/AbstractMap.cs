﻿namespace Mapbox.Unity.Map
{
	using System;
	using Mapbox.Unity.MeshGeneration;
	using Mapbox.Unity.Utilities;
	using Utils;
	using UnityEngine;
	using Mapbox.Map;

	// TODO: make abstract! For example: MapFromFile, MapFromLocationProvider, etc.
	public class AbstractMap : MonoBehaviour, IMap
	{
		[Geocode]
		[SerializeField]
		internal string _latitudeLongitudeString;

		[SerializeField]
		[Range(0, 22)]
		int _zoom;
		public int Zoom
		{
			get
			{
				return _zoom;
			}
			set
			{
				_zoom = value;
			}
		}

		[SerializeField]
		Transform _root;
		public Transform Root
		{
			get
			{
				return _root;
			}
		}

		[SerializeField]
		internal AbstractTileProvider _tileProvider;

		[SerializeField]
		AbstractMapVisualizer _mapVisualizer;
		public AbstractMapVisualizer MapVisualizer { get { return _mapVisualizer; } }

		[SerializeField]
		internal float _unityTileSize = 100;
		[SerializeField]
		bool _snapMapHeightToZero = true;

		MapboxAccess _fileSouce;

		Vector2d _mapCenterLatitudeLongitude;
		public Vector2d CenterLatitudeLongitude
		{
			get
			{
				return _mapCenterLatitudeLongitude;
			}
			set
			{
				_latitudeLongitudeString = string.Format("{0}, {1}", value.x, value.y);
				_mapCenterLatitudeLongitude = value;
			}
		}

		Vector2d _mapCenterMercator;
		public Vector2d CenterMercator
		{
			get
			{
				return _mapCenterMercator;
			}
		}

		float _worldRelativeScale;
		public float WorldRelativeScale
		{
			get
			{
				return _worldRelativeScale;
			}
		}

		bool _worldHeightFixed = false;

		public event Action OnInitialized = delegate { };

		protected virtual void Awake()
		{
			_worldHeightFixed = false;
			_fileSouce = MapboxAccess.Instance;
			_tileProvider.OnTileAdded += TileProvider_OnTileAdded;
			_tileProvider.OnTileRemoved += TileProvider_OnTileRemoved;
			if (!_root)
			{
				_root = transform;
			}
		}

		protected virtual void OnDestroy()
		{
			if (_tileProvider != null)
			{
				_tileProvider.OnTileAdded -= TileProvider_OnTileAdded;
				_tileProvider.OnTileRemoved -= TileProvider_OnTileRemoved;
			}

			_mapVisualizer.Destroy();
		}

		// This is the part that is abstract?
		protected virtual void Start()
		{
			var latLonSplit = _latitudeLongitudeString.Split(',');
			_mapCenterLatitudeLongitude = new Vector2d(double.Parse(latLonSplit[0]), double.Parse(latLonSplit[1]));

			var referenceTileRect = Conversions.TileBounds(TileCover.CoordinateToTileId(_mapCenterLatitudeLongitude, _zoom));
			_mapCenterMercator = referenceTileRect.Center;

			_worldRelativeScale = (float)(_unityTileSize / referenceTileRect.Size.x);
			//Root.localScale = Vector3.one * _worldRelativeScale;

			_mapVisualizer.Initialize(this, _fileSouce);
			_tileProvider.Initialize(this);

			OnInitialized();
		}

		void TileProvider_OnTileAdded(UnwrappedTileId tileId)
		{
			if (_snapMapHeightToZero && !_worldHeightFixed)
			{
				_worldHeightFixed = true;
				var tile = _mapVisualizer.LoadTile(tileId);
				if(tile.HeightDataState == MeshGeneration.Enums.TilePropertyState.Loaded)
				{
					var h = tile.QueryHeightData(.5f, .5f);
					Root.transform.position = new Vector3(
					 Root.transform.position.x,
					 -h * WorldRelativeScale,
					 Root.transform.position.z);
				}
				else
				{
					tile.OnHeightDataChanged += (s) =>
					{
						var h = s.QueryHeightData(.5f, .5f);
						Root.transform.position = new Vector3(
						 Root.transform.position.x,
						 -h * WorldRelativeScale,
						 Root.transform.position.z);
					};
				}
			}
			else
			{
				_mapVisualizer.LoadTile(tileId);
			}
		}

		void TileProvider_OnTileRemoved(UnwrappedTileId tileId)
		{
			_mapVisualizer.DisposeTile(tileId);
		}
	}
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Raftipelago.Data
{
    public class SpriteManager
	{
		private static Sprite _archipelagoSprite;
		public Sprite GetArchipelagoSprite()
        {
			if (_archipelagoSprite == null)
			{
				var texture = new Texture2D(2, 2);
				var allBytes = ComponentManager<EmbeddedFileUtils>.Value.ReadRawFile("Data", "Archipelago.png");
				if (texture.LoadImage(allBytes))
				{
					_archipelagoSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0, 0), 100.0f);
				}
			}
			return _archipelagoSprite;
		}
    }
}
